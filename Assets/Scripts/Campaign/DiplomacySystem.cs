using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Central diplomacy system managing all diplomatic actions between factions.
    /// Handles treaties, war declarations, alliances, and diplomatic AI evaluation.
    /// </summary>
    public class DiplomacySystem
    {
        private static DiplomacySystem _instance;
        public static DiplomacySystem Instance => _instance ??= new DiplomacySystem();

        // Active treaties between factions
        private Dictionary<string, Treaty> activeTreaties = new Dictionary<string, Treaty>();
        
        // Pending diplomatic offers
        private List<DiplomaticOffer> pendingOffers = new List<DiplomaticOffer>();
        
        // Diplomatic history for AI evaluation
        private List<DiplomaticHistoryEntry> history = new List<DiplomaticHistoryEntry>();

        // Events
        public delegate void DiplomacyEvent(FactionType from, FactionType to, DiplomaticActionType action);
        public event DiplomacyEvent OnDiplomaticAction;
        
        public delegate void OfferReceived(DiplomaticOffer offer);
        public event OfferReceived OnOfferReceived;

        public List<DiplomaticOffer> PendingOffers => pendingOffers;
        public Dictionary<string, Treaty> ActiveTreaties => activeTreaties;

        #region Diplomatic Actions

        /// <summary>
        /// Declare war on another faction
        /// </summary>
        public bool DeclareWar(FactionType aggressor, FactionType target)
        {
            if (aggressor == target) return false;
            
            var cm = CampaignManager.Instance;
            if (cm == null) return false;

            var aggressorFaction = cm.Factions[aggressor];
            var targetFaction = cm.Factions[target];

            // Check if already at war
            if (GetRelationState(aggressor, target) == DiplomacyState.War)
                return false;

            // Break any existing treaties
            BreakAllTreatiesBetween(aggressor, target);

            // Set war state
            SetRelationState(aggressor, target, DiplomacyState.War);
            
            // Record history
            AddHistoryEntry(aggressor, target, DiplomaticActionType.DeclareWar, 
                $"{aggressor} declared war on {target}");

            // Notify allies
            NotifyAlliesOfWar(aggressor, target);

            OnDiplomaticAction?.Invoke(aggressor, target, DiplomaticActionType.DeclareWar);
            
            Debug.Log($"[Diplomacy] {aggressor} declared war on {target}!");
            return true;
        }

        /// <summary>
        /// Propose peace to end a war
        /// </summary>
        public void ProposePeace(FactionType proposer, FactionType target, PeaceTerms terms)
        {
            if (GetRelationState(proposer, target) != DiplomacyState.War)
                return;

            var offer = new DiplomaticOffer
            {
                offerId = System.Guid.NewGuid().ToString(),
                fromFaction = proposer,
                toFaction = target,
                actionType = DiplomaticActionType.ProposePeace,
                peaceTerms = terms,
                turnProposed = CampaignManager.Instance?.CurrentTurn ?? 0,
                expiresInTurns = 3
            };

            SendOffer(offer);
        }

        /// <summary>
        /// Propose an alliance (defensive or offensive)
        /// </summary>
        public void ProposeAlliance(FactionType proposer, FactionType target, AllianceType allianceType)
        {
            if (GetRelationState(proposer, target) == DiplomacyState.War)
                return;

            var offer = new DiplomaticOffer
            {
                offerId = System.Guid.NewGuid().ToString(),
                fromFaction = proposer,
                toFaction = target,
                actionType = DiplomaticActionType.ProposeAlliance,
                allianceType = allianceType,
                turnProposed = CampaignManager.Instance?.CurrentTurn ?? 0,
                expiresInTurns = 5
            };

            SendOffer(offer);
        }

        /// <summary>
        /// Request military access through another faction's territory
        /// </summary>
        public void RequestMilitaryAccess(FactionType requester, FactionType target)
        {
            if (GetRelationState(requester, target) == DiplomacyState.War)
                return;

            var offer = new DiplomaticOffer
            {
                offerId = System.Guid.NewGuid().ToString(),
                fromFaction = requester,
                toFaction = target,
                actionType = DiplomaticActionType.RequestMilitaryAccess,
                turnProposed = CampaignManager.Instance?.CurrentTurn ?? 0,
                expiresInTurns = 3
            };

            SendOffer(offer);
        }

        /// <summary>
        /// Propose a trade agreement
        /// </summary>
        public void ProposeTradeAgreement(FactionType proposer, FactionType target, float goldPerTurn)
        {
            if (GetRelationState(proposer, target) == DiplomacyState.War)
                return;

            var offer = new DiplomaticOffer
            {
                offerId = System.Guid.NewGuid().ToString(),
                fromFaction = proposer,
                toFaction = target,
                actionType = DiplomaticActionType.ProposeTradeAgreement,
                goldAmount = goldPerTurn,
                turnProposed = CampaignManager.Instance?.CurrentTurn ?? 0,
                expiresInTurns = 5
            };

            SendOffer(offer);
        }

        /// <summary>
        /// Demand vassalization (for much weaker factions)
        /// </summary>
        public void DemandVassalization(FactionType overlord, FactionType target)
        {
            var offer = new DiplomaticOffer
            {
                offerId = System.Guid.NewGuid().ToString(),
                fromFaction = overlord,
                toFaction = target,
                actionType = DiplomaticActionType.DemandVassalization,
                turnProposed = CampaignManager.Instance?.CurrentTurn ?? 0,
                expiresInTurns = 2
            };

            SendOffer(offer);
        }

        /// <summary>
        /// Break an existing alliance
        /// </summary>
        public bool BreakAlliance(FactionType breaker, FactionType ally)
        {
            string treatyKey = GetTreatyKey(breaker, ally, TreatyType.Alliance);
            if (!activeTreaties.ContainsKey(treatyKey))
                return false;

            activeTreaties.Remove(treatyKey);
            
            // Reduce relations significantly
            ModifyRelationScore(breaker, ally, -50);
            SetRelationState(breaker, ally, DiplomacyState.Hostile);

            AddHistoryEntry(breaker, ally, DiplomaticActionType.BreakAlliance,
                $"{breaker} broke alliance with {ally}");

            OnDiplomaticAction?.Invoke(breaker, ally, DiplomaticActionType.BreakAlliance);
            
            Debug.Log($"[Diplomacy] {breaker} broke alliance with {ally}!");
            return true;
        }

        /// <summary>
        /// Propose a non-aggression pact (costs 30 PP)
        /// </summary>
        public DiplomacyResult ProposeNonAggression(FactionType proposer, FactionType target)
        {
            if (GetRelationState(proposer, target) == DiplomacyState.War)
                return DiplomacyResult.Failure(DiplomacyFailReason.AlreadyAtWar, "Impossible en temps de guerre");

            if (HasTreaty(proposer, target, TreatyType.NonAggression))
                return DiplomacyResult.Failure(DiplomacyFailReason.TreatyExists, "Pacte déjà existant");

            var cm = CampaignManager.Instance;
            if (cm == null || !cm.Factions[proposer].SpendPoliticalPower(30f))
                return DiplomacyResult.Failure(DiplomacyFailReason.InsufficientGold, "Pouvoir politique insuffisant (30 PP)");

            var offer = new DiplomaticOffer
            {
                offerId = System.Guid.NewGuid().ToString(),
                fromFaction = proposer,
                toFaction = target,
                actionType = DiplomaticActionType.ProposeNonAggression,
                turnProposed = cm.CurrentTurn,
                expiresInTurns = 5
            };
            SendOffer(offer);
            return DiplomacyResult.Success("Pacte de non-agression proposé");
        }

        /// <summary>
        /// Improve relations with a faction (costs 25 PP, +15 score)
        /// </summary>
        public DiplomacyResult ImproveRelations(FactionType actor, FactionType target)
        {
            if (GetRelationState(actor, target) == DiplomacyState.War)
                return DiplomacyResult.Failure(DiplomacyFailReason.AlreadyAtWar, "Impossible en temps de guerre");

            var cm = CampaignManager.Instance;
            if (cm == null || !cm.Factions[actor].SpendPoliticalPower(25f))
                return DiplomacyResult.Failure(DiplomacyFailReason.InsufficientGold, "Pouvoir politique insuffisant (25 PP)");

            ModifyRelationScore(actor, target, 15);

            AddHistoryEntry(actor, target, DiplomaticActionType.ImproveRelations,
                $"{actor} a amélioré les relations avec {target} (+15)");
            OnDiplomaticAction?.Invoke(actor, target, DiplomaticActionType.ImproveRelations);

            Debug.Log($"[Diplomacy] {actor} improved relations with {target} (+15)");
            return DiplomacyResult.Success("Relations améliorées (+15)");
        }

        /// <summary>
        /// Send a gift of gold to another faction (costs 200g, +20 score)
        /// </summary>
        public DiplomacyResult SendGift(FactionType sender, FactionType target, float amount = 200f)
        {
            if (GetRelationState(sender, target) == DiplomacyState.War)
                return DiplomacyResult.Failure(DiplomacyFailReason.AlreadyAtWar, "Impossible en temps de guerre");

            var cm = CampaignManager.Instance;
            if (cm == null || cm.Factions[sender].gold < amount)
                return DiplomacyResult.Failure(DiplomacyFailReason.InsufficientGold, $"Or insuffisant ({amount}g requis)");

            cm.Factions[sender].gold -= amount;
            cm.Factions[target].gold += amount;
            ModifyRelationScore(sender, target, 20);

            AddHistoryEntry(sender, target, DiplomaticActionType.SendGift,
                $"{sender} a envoyé un cadeau de {amount}g à {target} (+20)");
            OnDiplomaticAction?.Invoke(sender, target, DiplomaticActionType.SendGift);

            Debug.Log($"[Diplomacy] {sender} sent {amount}g gift to {target} (+20)");
            return DiplomacyResult.Success($"Cadeau envoyé: {amount}g (+20 relation)");
        }

        /// <summary>
        /// Insult a faction (-25 score to them, +5% war support for us)
        /// </summary>
        public DiplomacyResult InsultFaction(FactionType insulter, FactionType target)
        {
            if (GetRelationState(insulter, target) == DiplomacyState.Alliance)
                return DiplomacyResult.Failure(DiplomacyFailReason.AlreadyAllied, "Impossible d'insulter un allié");

            var cm = CampaignManager.Instance;
            if (cm == null) return DiplomacyResult.Failure(DiplomacyFailReason.None, "Erreur");

            ModifyRelationScore(insulter, target, -25);
            cm.Factions[insulter].warSupport = Mathf.Clamp01(cm.Factions[insulter].warSupport + 0.05f);

            AddHistoryEntry(insulter, target, DiplomaticActionType.InsultFaction,
                $"{insulter} a insulté {target} (-25 relation, +5% soutien de guerre)");
            OnDiplomaticAction?.Invoke(insulter, target, DiplomaticActionType.InsultFaction);

            Debug.Log($"[Diplomacy] {insulter} insulted {target} (-25 relation, +5% war support)");
            return DiplomacyResult.Success("Insulte envoyée (-25 relation, +5% soutien de guerre)");
        }

        /// <summary>
        /// Guarantee independence of a faction (costs 50 PP, join war if they are attacked)
        /// </summary>
        public DiplomacyResult GuaranteeIndependence(FactionType guarantor, FactionType target)
        {
            if (GetRelationState(guarantor, target) == DiplomacyState.War)
                return DiplomacyResult.Failure(DiplomacyFailReason.AlreadyAtWar, "Impossible en temps de guerre");

            if (HasTreaty(guarantor, target, TreatyType.Guarantee))
                return DiplomacyResult.Failure(DiplomacyFailReason.TreatyExists, "Garantie déjà existante");

            var cm = CampaignManager.Instance;
            if (cm == null || !cm.Factions[guarantor].SpendPoliticalPower(50f))
                return DiplomacyResult.Failure(DiplomacyFailReason.InsufficientGold, "Pouvoir politique insuffisant (50 PP)");

            string treatyKey = GetTreatyKey(guarantor, target, TreatyType.Guarantee);
            var treaty = new Treaty
            {
                treatyId = treatyKey,
                type = TreatyType.Guarantee,
                factions = new List<FactionType> { guarantor, target },
                turnSigned = cm.CurrentTurn
            };
            activeTreaties[treatyKey] = treaty;
            ModifyRelationScore(guarantor, target, 15);

            AddHistoryEntry(guarantor, target, DiplomaticActionType.GuaranteeIndependence,
                $"{guarantor} garantit l'indépendance de {target}");
            OnDiplomaticAction?.Invoke(guarantor, target, DiplomaticActionType.GuaranteeIndependence);

            Debug.Log($"[Diplomacy] {guarantor} guaranteed independence of {target}");
            return DiplomacyResult.Success("Indépendance garantie");
        }

        /// <summary>
        /// Get PP cost for a diplomatic action
        /// </summary>
        public static int GetActionPPCost(DiplomaticActionType action) => action switch
        {
            DiplomaticActionType.DeclareWar => 20,
            DiplomaticActionType.ProposePeace => 10,
            DiplomaticActionType.ProposeAlliance => 35,
            DiplomaticActionType.RequestMilitaryAccess => 20,
            DiplomaticActionType.ProposeTradeAgreement => 15,
            DiplomaticActionType.DemandVassalization => 100,
            DiplomaticActionType.ProposeNonAggression => 30,
            DiplomaticActionType.ImproveRelations => 25,
            DiplomaticActionType.GuaranteeIndependence => 50,
            DiplomaticActionType.ProposeMarriage => 40,
            _ => 0
        };

        #endregion

        #region Offer Management

        private void SendOffer(DiplomaticOffer offer)
        {
            pendingOffers.Add(offer);
            OnOfferReceived?.Invoke(offer);
            
            Debug.Log($"[Diplomacy] {offer.fromFaction} sent {offer.actionType} offer to {offer.toFaction}");
        }

        /// <summary>
        /// Accept a diplomatic offer
        /// </summary>
        public bool AcceptOffer(string offerId)
        {
            var offer = pendingOffers.Find(o => o.offerId == offerId);
            if (offer == null) return false;

            bool success = ProcessAcceptedOffer(offer);
            pendingOffers.Remove(offer);
            
            return success;
        }

        /// <summary>
        /// Reject a diplomatic offer
        /// </summary>
        public void RejectOffer(string offerId)
        {
            var offer = pendingOffers.Find(o => o.offerId == offerId);
            if (offer == null) return;

            // Slightly reduce relations for rejection
            ModifyRelationScore(offer.fromFaction, offer.toFaction, -5);
            
            AddHistoryEntry(offer.toFaction, offer.fromFaction, DiplomaticActionType.RejectOffer,
                $"{offer.toFaction} rejected {offer.actionType} from {offer.fromFaction}");

            pendingOffers.Remove(offer);
            
            Debug.Log($"[Diplomacy] {offer.toFaction} rejected {offer.actionType} from {offer.fromFaction}");
        }

        private bool ProcessAcceptedOffer(DiplomaticOffer offer)
        {
            switch (offer.actionType)
            {
                case DiplomaticActionType.ProposePeace:
                    return AcceptPeace(offer);
                    
                case DiplomaticActionType.ProposeAlliance:
                    return FormAlliance(offer.fromFaction, offer.toFaction, offer.allianceType);
                    
                case DiplomaticActionType.RequestMilitaryAccess:
                    return GrantMilitaryAccess(offer.fromFaction, offer.toFaction);
                    
                case DiplomaticActionType.ProposeTradeAgreement:
                    return FormTradeAgreement(offer.fromFaction, offer.toFaction, offer.goldAmount);
                    
                case DiplomaticActionType.DemandVassalization:
                    return BecomeVassal(offer.toFaction, offer.fromFaction);

                case DiplomaticActionType.ProposeNonAggression:
                    return FormNonAggressionPact(offer.fromFaction, offer.toFaction);
                    
                default:
                    return false;
            }
        }

        private bool FormNonAggressionPact(FactionType f1, FactionType f2)
        {
            string treatyKey = GetTreatyKey(f1, f2, TreatyType.NonAggression);
            if (activeTreaties.ContainsKey(treatyKey)) return false;

            var treaty = new Treaty
            {
                treatyId = treatyKey,
                type = TreatyType.NonAggression,
                factions = new List<FactionType> { f1, f2 },
                turnSigned = CampaignManager.Instance?.CurrentTurn ?? 0,
                duration = 10
            };

            activeTreaties[treatyKey] = treaty;
            ModifyRelationScore(f1, f2, 10);

            AddHistoryEntry(f1, f2, DiplomaticActionType.ProposeNonAggression,
                $"Pacte de non-agression signé entre {f1} et {f2}");

            Debug.Log($"[Diplomacy] Non-aggression pact formed between {f1} and {f2}");
            return true;
        }

        private bool AcceptPeace(DiplomaticOffer offer)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return false;

            // Apply peace terms
            if (offer.peaceTerms != null)
            {
                // Transfer gold
                if (offer.peaceTerms.goldPayment > 0)
                {
                    cm.Factions[offer.peaceTerms.payingFaction].gold -= offer.peaceTerms.goldPayment;
                    cm.Factions[offer.peaceTerms.receivingFaction].gold += offer.peaceTerms.goldPayment;
                }

                // Transfer provinces
                foreach (var provinceId in offer.peaceTerms.provincesToCede)
                {
                    if (cm.Provinces.ContainsKey(provinceId))
                    {
                        cm.TransferProvince(provinceId, offer.peaceTerms.receivingFaction);
                    }
                }
            }

            // End war state
            SetRelationState(offer.fromFaction, offer.toFaction, DiplomacyState.Neutral);
            
            // Reset war turns counter
            var relation = GetRelation(offer.fromFaction, offer.toFaction);
            if (relation != null) relation.turnsAtWar = 0;

            AddHistoryEntry(offer.fromFaction, offer.toFaction, DiplomaticActionType.AcceptPeace,
                $"Peace established between {offer.fromFaction} and {offer.toFaction}");

            OnDiplomaticAction?.Invoke(offer.fromFaction, offer.toFaction, DiplomaticActionType.AcceptPeace);
            
            Debug.Log($"[Diplomacy] Peace established between {offer.fromFaction} and {offer.toFaction}!");
            return true;
        }

        private bool FormAlliance(FactionType faction1, FactionType faction2, AllianceType type)
        {
            string treatyKey = GetTreatyKey(faction1, faction2, TreatyType.Alliance);
            
            if (activeTreaties.ContainsKey(treatyKey))
                return false;

            var treaty = new Treaty
            {
                treatyId = treatyKey,
                type = TreatyType.Alliance,
                factions = new List<FactionType> { faction1, faction2 },
                allianceType = type,
                turnSigned = CampaignManager.Instance?.CurrentTurn ?? 0
            };

            activeTreaties[treatyKey] = treaty;
            SetRelationState(faction1, faction2, DiplomacyState.Alliance);
            ModifyRelationScore(faction1, faction2, 30);

            AddHistoryEntry(faction1, faction2, DiplomaticActionType.FormAlliance,
                $"{type} alliance formed between {faction1} and {faction2}");

            OnDiplomaticAction?.Invoke(faction1, faction2, DiplomaticActionType.FormAlliance);
            
            Debug.Log($"[Diplomacy] {type} alliance formed between {faction1} and {faction2}!");
            return true;
        }

        private bool GrantMilitaryAccess(FactionType requester, FactionType granter)
        {
            string treatyKey = GetTreatyKey(requester, granter, TreatyType.MilitaryAccess);
            
            if (activeTreaties.ContainsKey(treatyKey))
                return false;

            var treaty = new Treaty
            {
                treatyId = treatyKey,
                type = TreatyType.MilitaryAccess,
                factions = new List<FactionType> { requester, granter },
                turnSigned = CampaignManager.Instance?.CurrentTurn ?? 0,
                duration = 10 // 10 turns
            };

            activeTreaties[treatyKey] = treaty;
            ModifyRelationScore(requester, granter, 10);

            Debug.Log($"[Diplomacy] {granter} granted military access to {requester}");
            return true;
        }

        private bool FormTradeAgreement(FactionType faction1, FactionType faction2, float goldPerTurn)
        {
            string treatyKey = GetTreatyKey(faction1, faction2, TreatyType.TradeAgreement);
            
            if (activeTreaties.ContainsKey(treatyKey))
                return false;

            var treaty = new Treaty
            {
                treatyId = treatyKey,
                type = TreatyType.TradeAgreement,
                factions = new List<FactionType> { faction1, faction2 },
                goldPerTurn = goldPerTurn,
                turnSigned = CampaignManager.Instance?.CurrentTurn ?? 0
            };

            activeTreaties[treatyKey] = treaty;
            
            if (GetRelationState(faction1, faction2) == DiplomacyState.Neutral)
                SetRelationState(faction1, faction2, DiplomacyState.Friendly);
            
            ModifyRelationScore(faction1, faction2, 15);

            Debug.Log($"[Diplomacy] Trade agreement formed between {faction1} and {faction2}");
            return true;
        }

        private bool BecomeVassal(FactionType vassal, FactionType overlord)
        {
            string treatyKey = GetTreatyKey(vassal, overlord, TreatyType.Vassalage);
            
            var treaty = new Treaty
            {
                treatyId = treatyKey,
                type = TreatyType.Vassalage,
                factions = new List<FactionType> { vassal, overlord },
                overlord = overlord,
                vassal = vassal,
                turnSigned = CampaignManager.Instance?.CurrentTurn ?? 0
            };

            activeTreaties[treatyKey] = treaty;
            SetRelationState(vassal, overlord, DiplomacyState.Alliance);

            AddHistoryEntry(vassal, overlord, DiplomaticActionType.BecomeVassal,
                $"{vassal} became vassal of {overlord}");

            Debug.Log($"[Diplomacy] {vassal} became vassal of {overlord}!");
            return true;
        }

        #endregion

        #region Relation Management

        public DiplomacyState GetRelationState(FactionType faction1, FactionType faction2)
        {
            var relation = GetRelation(faction1, faction2);
            return relation?.state ?? DiplomacyState.Neutral;
        }

        public void SetRelationState(FactionType faction1, FactionType faction2, DiplomacyState state)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            if (!cm.Factions[faction1].relations.ContainsKey(faction2))
                cm.Factions[faction1].relations[faction2] = new DiplomaticRelation();
            if (!cm.Factions[faction2].relations.ContainsKey(faction1))
                cm.Factions[faction2].relations[faction1] = new DiplomaticRelation();

            cm.Factions[faction1].relations[faction2].state = state;
            cm.Factions[faction2].relations[faction1].state = state;
        }

        public DiplomaticRelation GetRelation(FactionType faction1, FactionType faction2)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return null;

            if (cm.Factions.TryGetValue(faction1, out var f1) && 
                f1.relations.TryGetValue(faction2, out var relation))
            {
                return relation;
            }
            return null;
        }

        public void ModifyRelationScore(FactionType faction1, FactionType faction2, int amount)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            if (!cm.Factions[faction1].relations.ContainsKey(faction2))
                cm.Factions[faction1].relations[faction2] = new DiplomaticRelation();
            if (!cm.Factions[faction2].relations.ContainsKey(faction1))
                cm.Factions[faction2].relations[faction1] = new DiplomaticRelation();

            cm.Factions[faction1].relations[faction2].relationScore += amount;
            cm.Factions[faction2].relations[faction1].relationScore += amount;

            // Clamp to -100 to 100
            cm.Factions[faction1].relations[faction2].relationScore = 
                Mathf.Clamp(cm.Factions[faction1].relations[faction2].relationScore, -100, 100);
            cm.Factions[faction2].relations[faction1].relationScore = 
                Mathf.Clamp(cm.Factions[faction2].relations[faction1].relationScore, -100, 100);
        }

        public int GetRelationScore(FactionType faction1, FactionType faction2)
        {
            var relation = GetRelation(faction1, faction2);
            return relation?.relationScore ?? 0;
        }

        #endregion

        #region Treaty Management

        public bool HasTreaty(FactionType faction1, FactionType faction2, TreatyType type)
        {
            string key = GetTreatyKey(faction1, faction2, type);
            return activeTreaties.ContainsKey(key);
        }

        public bool HasMilitaryAccess(FactionType requester, FactionType territory)
        {
            // Always have access to own territory
            if (requester == territory) return true;
            
            // Check for military access treaty
            if (HasTreaty(requester, territory, TreatyType.MilitaryAccess))
                return true;
            
            // Allies have mutual access
            if (HasTreaty(requester, territory, TreatyType.Alliance))
                return true;
            
            // Vassals and overlords have mutual access
            if (HasTreaty(requester, territory, TreatyType.Vassalage))
                return true;

            return false;
        }

        public List<FactionType> GetAllies(FactionType faction)
        {
            var allies = new List<FactionType>();
            
            foreach (var treaty in activeTreaties.Values)
            {
                if (treaty.type == TreatyType.Alliance && treaty.factions.Contains(faction))
                {
                    foreach (var f in treaty.factions)
                    {
                        if (f != faction && !allies.Contains(f))
                            allies.Add(f);
                    }
                }
            }
            
            return allies;
        }

        public bool IsVassal(FactionType faction)
        {
            foreach (var treaty in activeTreaties.Values)
            {
                if (treaty.type == TreatyType.Vassalage && treaty.vassal == faction)
                    return true;
            }
            return false;
        }

        public FactionType? GetOverlord(FactionType vassal)
        {
            foreach (var treaty in activeTreaties.Values)
            {
                if (treaty.type == TreatyType.Vassalage && treaty.vassal == vassal)
                    return treaty.overlord;
            }
            return null;
        }

        private string GetTreatyKey(FactionType f1, FactionType f2, TreatyType type)
        {
            // Ensure consistent ordering
            if (f1 > f2) (f1, f2) = (f2, f1);
            return $"{f1}_{f2}_{type}";
        }

        private void BreakAllTreatiesBetween(FactionType f1, FactionType f2)
        {
            var toRemove = new List<string>();
            
            foreach (var kvp in activeTreaties)
            {
                if (kvp.Value.factions.Contains(f1) && kvp.Value.factions.Contains(f2))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                activeTreaties.Remove(key);
            }
        }

        #endregion

        #region Turn Processing

        /// <summary>
        /// Process diplomacy at end of turn
        /// </summary>
        public void ProcessTurn()
        {
            // Expire old offers
            ExpireOffers();
            
            // Update war duration
            UpdateWarDuration();
            
            // Process trade income
            ProcessTradeIncome();
            
            // Check treaty durations
            CheckTreatyExpirations();
        }

        private void ExpireOffers()
        {
            int currentTurn = CampaignManager.Instance?.CurrentTurn ?? 0;
            
            pendingOffers.RemoveAll(offer => 
                currentTurn - offer.turnProposed >= offer.expiresInTurns);
        }

        private void UpdateWarDuration()
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            foreach (var faction in cm.Factions.Values)
            {
                foreach (var relation in faction.relations.Values)
                {
                    if (relation.state == DiplomacyState.War)
                    {
                        relation.turnsAtWar++;
                    }
                    else if (relation.state == DiplomacyState.Alliance)
                    {
                        relation.turnsOfAlliance++;
                    }
                }
            }
        }

        private void ProcessTradeIncome()
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            foreach (var treaty in activeTreaties.Values)
            {
                if (treaty.type == TreatyType.TradeAgreement && treaty.goldPerTurn > 0)
                {
                    // Both factions benefit from trade
                    foreach (var faction in treaty.factions)
                    {
                        if (cm.Factions.ContainsKey(faction))
                        {
                            cm.Factions[faction].gold += treaty.goldPerTurn;
                        }
                    }
                }
            }
        }

        private void CheckTreatyExpirations()
        {
            int currentTurn = CampaignManager.Instance?.CurrentTurn ?? 0;
            var toRemove = new List<string>();

            foreach (var kvp in activeTreaties)
            {
                if (kvp.Value.duration > 0 && 
                    currentTurn - kvp.Value.turnSigned >= kvp.Value.duration)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                var treaty = activeTreaties[key];
                activeTreaties.Remove(key);
                Debug.Log($"[Diplomacy] Treaty expired: {treaty.type} between {string.Join(", ", treaty.factions)}");
            }
        }

        #endregion

        #region Notifications

        private void NotifyAlliesOfWar(FactionType aggressor, FactionType target)
        {
            // Notify target's allies
            var targetAllies = GetAllies(target);
            foreach (var ally in targetAllies)
            {
                if (ally != aggressor)
                {
                    // Defensive allies join the war
                    var allianceTreaty = activeTreaties.Values.FirstOrDefault(t => 
                        t.type == TreatyType.Alliance && 
                        t.factions.Contains(target) && 
                        t.factions.Contains(ally));

                    if (allianceTreaty != null && 
                        (allianceTreaty.allianceType == AllianceType.Defensive || 
                         allianceTreaty.allianceType == AllianceType.Full))
                    {
                        DeclareWar(ally, aggressor);
                        Debug.Log($"[Diplomacy] {ally} joins war against {aggressor} (defensive alliance with {target})");
                    }
                }
            }
        }

        private Treaty FindTreaty(System.Func<Treaty, bool> predicate)
        {
            foreach (var treaty in activeTreaties.Values)
            {
                if (predicate(treaty))
                    return treaty;
            }
            return null;
        }

        #endregion

        #region History

        private void AddHistoryEntry(FactionType from, FactionType to, DiplomaticActionType action, string description)
        {
            history.Add(new DiplomaticHistoryEntry
            {
                turn = CampaignManager.Instance?.CurrentTurn ?? 0,
                fromFaction = from,
                toFaction = to,
                action = action,
                description = description
            });

            // Keep only last 100 entries
            if (history.Count > 100)
                history.RemoveAt(0);
        }

        public List<DiplomaticHistoryEntry> GetHistory(FactionType faction, int maxEntries = 20)
        {
            var result = new List<DiplomaticHistoryEntry>();
            
            for (int i = history.Count - 1; i >= 0 && result.Count < maxEntries; i--)
            {
                if (history[i].fromFaction == faction || history[i].toFaction == faction)
                {
                    result.Add(history[i]);
                }
            }
            
            return result;
        }

        #endregion

        #region AI Evaluation

        /// <summary>
        /// AI evaluates whether to accept a diplomatic offer
        /// </summary>
        public bool AIEvaluateOffer(DiplomaticOffer offer)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return false;

            float acceptanceScore = 0f;
            
            // Base score from current relations
            int relationScore = GetRelationScore(offer.fromFaction, offer.toFaction);
            acceptanceScore += relationScore * 0.5f;

            // Evaluate based on offer type
            switch (offer.actionType)
            {
                case DiplomaticActionType.ProposePeace:
                    acceptanceScore += EvaluatePeaceOffer(offer);
                    break;
                    
                case DiplomaticActionType.ProposeAlliance:
                    acceptanceScore += EvaluateAllianceOffer(offer);
                    break;
                    
                case DiplomaticActionType.RequestMilitaryAccess:
                    acceptanceScore += EvaluateMilitaryAccessRequest(offer);
                    break;
                    
                case DiplomaticActionType.ProposeTradeAgreement:
                    acceptanceScore += 20f; // Trade is generally good
                    break;
                    
                case DiplomaticActionType.DemandVassalization:
                    acceptanceScore += EvaluateVassalizationDemand(offer);
                    break;
            }

            // Random factor for unpredictability
            acceptanceScore += Random.Range(-10f, 10f);

            // Technology diplomacy bonus (from proposer's researched diplomacy techs)
            var proposerFaction = cm.Factions.ContainsKey(offer.fromFaction) ? cm.Factions[offer.fromFaction] : null;
            if (proposerFaction?.techTree != null)
                acceptanceScore += proposerFaction.techTree.TotalDiplomacyBonus * 0.3f;

            return acceptanceScore > 0f;
        }

        private float EvaluatePeaceOffer(DiplomaticOffer offer)
        {
            float score = 0f;
            var relation = GetRelation(offer.fromFaction, offer.toFaction);
            
            if (relation != null)
            {
                // Long wars make peace more attractive
                score += relation.turnsAtWar * 5f;
            }

            // Evaluate peace terms
            if (offer.peaceTerms != null)
            {
                // Receiving gold is good
                if (offer.peaceTerms.receivingFaction == offer.toFaction)
                    score += offer.peaceTerms.goldPayment * 0.01f;
                else
                    score -= offer.peaceTerms.goldPayment * 0.02f;

                // Losing provinces is bad
                score -= offer.peaceTerms.provincesToCede.Count * 30f;
            }

            return score;
        }

        private float EvaluateAllianceOffer(DiplomaticOffer offer)
        {
            float score = 0f;
            var cm = CampaignManager.Instance;
            
            // Check if we share enemies
            foreach (var faction in cm.Factions.Keys)
            {
                if (faction != offer.fromFaction && faction != offer.toFaction)
                {
                    bool weAtWar = GetRelationState(offer.toFaction, faction) == DiplomacyState.War;
                    bool theyAtWar = GetRelationState(offer.fromFaction, faction) == DiplomacyState.War;
                    
                    if (weAtWar && theyAtWar)
                        score += 30f; // Common enemy
                }
            }

            // Full alliance is more valuable but more risky
            if (offer.allianceType == AllianceType.Full)
                score += 10f;

            return score;
        }

        private float EvaluateMilitaryAccessRequest(DiplomaticOffer offer)
        {
            float score = 0f;
            
            // Generally neutral unless we're hostile
            var state = GetRelationState(offer.fromFaction, offer.toFaction);
            
            if (state == DiplomacyState.Friendly || state == DiplomacyState.Alliance)
                score += 30f;
            else if (state == DiplomacyState.Hostile)
                score -= 50f;

            return score;
        }

        private float EvaluateVassalizationDemand(DiplomaticOffer offer)
        {
            // Only accept if significantly weaker
            var cm = CampaignManager.Instance;
            if (cm == null) return -100f;

            int ourArmyStrength = CalculateFactionStrength(offer.toFaction);
            int theirArmyStrength = CalculateFactionStrength(offer.fromFaction);

            if (theirArmyStrength > ourArmyStrength * 3)
                return 20f; // They're much stronger, might as well submit
            
            return -100f; // Refuse vassalization
        }

        private int CalculateFactionStrength(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return 0;

            int strength = 0;
            
            foreach (var army in cm.Armies.Values)
            {
                if (army.faction == faction)
                {
                    strength += army.TotalSoldiers;
                }
            }

            // Add province count
            strength += cm.Factions[faction].ownedProvinceIds.Count * 100;

            return strength;
        }

        #endregion
    }
}
