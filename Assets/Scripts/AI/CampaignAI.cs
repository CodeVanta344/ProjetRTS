using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.AI
{
    /// <summary>
    /// Strategic AI for campaign map. Manages faction decisions for economy, military, and diplomacy.
    /// </summary>
    public class CampaignAI
    {
        private static CampaignAI _instance;
        public static CampaignAI Instance => _instance ??= new CampaignAI();

        // AI personality per faction
        private Dictionary<FactionType, AIPersonality> personalities = new Dictionary<FactionType, AIPersonality>();

        // Threat assessment cache
        private Dictionary<FactionType, Dictionary<FactionType, float>> threatLevels = 
            new Dictionary<FactionType, Dictionary<FactionType, float>>();

        public void Initialize()
        {
            SetupPersonalities();
        }

        private void SetupPersonalities()
        {
            // France - Aggressive expansionist
            personalities[FactionType.France] = new AIPersonality
            {
                aggressiveness = 0.8f,
                expansionism = 0.9f,
                diplomacyFocus = 0.4f,
                economyFocus = 0.5f,
                navalFocus = 0.3f,
                preferredTargets = new List<FactionType> { FactionType.Austria, FactionType.Prussia, FactionType.Russia }
            };

            // Britain - Naval power, defensive on land
            personalities[FactionType.Britain] = new AIPersonality
            {
                aggressiveness = 0.5f,
                expansionism = 0.6f,
                diplomacyFocus = 0.7f,
                economyFocus = 0.8f,
                navalFocus = 0.95f,
                preferredTargets = new List<FactionType> { FactionType.France, FactionType.Spain }
            };

            // Prussia - Military focused
            personalities[FactionType.Prussia] = new AIPersonality
            {
                aggressiveness = 0.7f,
                expansionism = 0.6f,
                diplomacyFocus = 0.5f,
                economyFocus = 0.6f,
                navalFocus = 0.1f,
                preferredTargets = new List<FactionType> { FactionType.Austria, FactionType.France }
            };

            // Russia - Defensive, slow expansion
            personalities[FactionType.Russia] = new AIPersonality
            {
                aggressiveness = 0.4f,
                expansionism = 0.5f,
                diplomacyFocus = 0.6f,
                economyFocus = 0.7f,
                navalFocus = 0.2f,
                preferredTargets = new List<FactionType> { FactionType.Ottoman, FactionType.Prussia }
            };

            // Austria - Defensive, diplomatic
            personalities[FactionType.Austria] = new AIPersonality
            {
                aggressiveness = 0.4f,
                expansionism = 0.4f,
                diplomacyFocus = 0.8f,
                economyFocus = 0.7f,
                navalFocus = 0.1f,
                preferredTargets = new List<FactionType> { FactionType.Ottoman, FactionType.Prussia }
            };

            // Spain - Colonial defense
            personalities[FactionType.Spain] = new AIPersonality
            {
                aggressiveness = 0.3f,
                expansionism = 0.3f,
                diplomacyFocus = 0.6f,
                economyFocus = 0.7f,
                navalFocus = 0.6f,
                preferredTargets = new List<FactionType> { FactionType.Britain }
            };

            // Ottoman - Defensive, regional power
            personalities[FactionType.Ottoman] = new AIPersonality
            {
                aggressiveness = 0.5f,
                expansionism = 0.4f,
                diplomacyFocus = 0.5f,
                economyFocus = 0.6f,
                navalFocus = 0.4f,
                preferredTargets = new List<FactionType> { FactionType.Russia, FactionType.Austria }
            };
        }

        #region Turn Processing

        /// <summary>
        /// Process AI turn for a faction
        /// </summary>
        public void ProcessFactionTurn(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;
            if (!cm.Factions.ContainsKey(faction)) return;
            if (cm.Factions[faction].isEliminated) return;

            // Update threat assessment
            UpdateThreatAssessment(faction);

            // Make decisions in order of priority
            ProcessDiplomacy(faction);
            ProcessEconomy(faction);
            ProcessMilitary(faction);
            ProcessResearch(faction);
        }

        #endregion

        #region Threat Assessment

        private void UpdateThreatAssessment(FactionType faction)
        {
            if (!threatLevels.ContainsKey(faction))
                threatLevels[faction] = new Dictionary<FactionType, float>();

            var cm = CampaignManager.Instance;
            var myFaction = cm.Factions[faction];

            foreach (var otherFaction in cm.Factions.Keys)
            {
                if (otherFaction == faction) continue;
                if (cm.Factions[otherFaction].isEliminated) continue;

                float threat = CalculateThreat(faction, otherFaction);
                threatLevels[faction][otherFaction] = threat;
            }
        }

        private float CalculateThreat(FactionType us, FactionType them)
        {
            var cm = CampaignManager.Instance;
            float threat = 0f;

            // Military strength comparison
            int ourStrength = CalculateMilitaryStrength(us);
            int theirStrength = CalculateMilitaryStrength(them);
            
            if (theirStrength > ourStrength)
                threat += (theirStrength - ourStrength) / 100f;

            // Border proximity
            int sharedBorders = CountSharedBorders(us, them);
            threat += sharedBorders * 10f;

            // Current relations
            var diplomacy = DiplomacySystem.Instance;
            if (diplomacy != null)
            {
                var state = diplomacy.GetRelationState(us, them);
                switch (state)
                {
                    case DiplomacyState.War:
                        threat += 50f;
                        break;
                    case DiplomacyState.Hostile:
                        threat += 25f;
                        break;
                    case DiplomacyState.Alliance:
                        threat -= 30f;
                        break;
                }
            }

            // Historical grievances
            // (simplified - would track past wars, broken treaties, etc.)

            return Mathf.Max(0f, threat);
        }

        private int CalculateMilitaryStrength(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            int strength = 0;

            foreach (var army in cm.Armies.Values)
            {
                if (army.faction == faction)
                    strength += army.TotalSoldiers;
            }

            // Add garrison strength
            foreach (var city in cm.Cities.Values)
            {
                if (city.owner == faction)
                    strength += city.garrisonSize;
            }

            return strength;
        }

        private int CountSharedBorders(FactionType faction1, FactionType faction2)
        {
            var cm = CampaignManager.Instance;
            int shared = 0;

            foreach (var province in cm.Provinces.Values)
            {
                if (province.owner != faction1) continue;

                foreach (var neighborId in province.neighborIds)
                {
                    if (cm.Provinces.TryGetValue(neighborId, out var neighbor))
                    {
                        if (neighbor.owner == faction2)
                            shared++;
                    }
                }
            }

            return shared;
        }

        #endregion

        #region Diplomacy AI

        private void ProcessDiplomacy(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            var diplomacy = DiplomacySystem.Instance;
            var personality = personalities[faction];

            // Process pending offers
            ProcessPendingOffers(faction);

            // Consider new diplomatic actions
            foreach (var otherFaction in cm.Factions.Keys)
            {
                if (otherFaction == faction) continue;
                if (cm.Factions[otherFaction].isEliminated) continue;

                var state = diplomacy.GetRelationState(faction, otherFaction);
                float threat = threatLevels[faction].GetValueOrDefault(otherFaction, 0f);

                // Consider declaring war
                if (ShouldDeclareWar(faction, otherFaction, personality, threat))
                {
                    diplomacy.DeclareWar(faction, otherFaction);
                    continue;
                }

                // Consider proposing peace
                if (state == DiplomacyState.War && ShouldProposePeace(faction, otherFaction))
                {
                    var terms = CreatePeaceTerms(faction, otherFaction);
                    diplomacy.ProposePeace(faction, otherFaction, terms);
                    continue;
                }

                // Consider alliance
                if (state != DiplomacyState.War && ShouldProposeAlliance(faction, otherFaction, personality))
                {
                    diplomacy.ProposeAlliance(faction, otherFaction, AllianceType.Defensive);
                }
            }

            // Form coalitions against dominant power
            ConsiderCoalition(faction);
        }

        private void ProcessPendingOffers(FactionType faction)
        {
            var diplomacy = DiplomacySystem.Instance;
            var offersToProcess = new List<DiplomaticOffer>();

            foreach (var offer in diplomacy.PendingOffers)
            {
                if (offer.toFaction == faction)
                    offersToProcess.Add(offer);
            }

            foreach (var offer in offersToProcess)
            {
                bool accept = diplomacy.AIEvaluateOffer(offer);
                if (accept)
                    diplomacy.AcceptOffer(offer.offerId);
                else
                    diplomacy.RejectOffer(offer.offerId);
            }
        }

        private bool ShouldDeclareWar(FactionType us, FactionType them, AIPersonality personality, float threat)
        {
            var cm = CampaignManager.Instance;
            var diplomacy = DiplomacySystem.Instance;

            // Don't declare war on allies
            if (diplomacy.HasTreaty(us, them, TreatyType.Alliance))
                return false;

            // Already at war
            if (diplomacy.GetRelationState(us, them) == DiplomacyState.War)
                return false;

            // Check if we're strong enough
            int ourStrength = CalculateMilitaryStrength(us);
            int theirStrength = CalculateMilitaryStrength(them);

            if (ourStrength < theirStrength * 0.8f)
                return false; // Too weak

            // Personality check
            float warChance = personality.aggressiveness * 0.1f;
            
            // Preferred target bonus
            if (personality.preferredTargets.Contains(them))
                warChance += 0.1f;

            // Opportunity - they're at war with someone else
            foreach (var otherFaction in cm.Factions.Keys)
            {
                if (otherFaction != us && otherFaction != them)
                {
                    if (diplomacy.GetRelationState(them, otherFaction) == DiplomacyState.War)
                        warChance += 0.05f;
                }
            }

            return Random.value < warChance;
        }

        private bool ShouldProposePeace(FactionType us, FactionType them)
        {
            var diplomacy = DiplomacySystem.Instance;
            var relation = diplomacy.GetRelation(us, them);

            if (relation == null) return false;

            // Long war exhaustion
            if (relation.turnsAtWar > 10)
                return Random.value < 0.3f;

            // We're losing
            int ourStrength = CalculateMilitaryStrength(us);
            int theirStrength = CalculateMilitaryStrength(them);

            if (ourStrength < theirStrength * 0.5f)
                return Random.value < 0.5f;

            return false;
        }

        private PeaceTerms CreatePeaceTerms(FactionType proposer, FactionType target)
        {
            int ourStrength = CalculateMilitaryStrength(proposer);
            int theirStrength = CalculateMilitaryStrength(target);

            var terms = new PeaceTerms();

            if (ourStrength > theirStrength)
            {
                // We're winning - demand concessions
                terms.payingFaction = target;
                terms.receivingFaction = proposer;
                terms.goldPayment = (ourStrength - theirStrength) * 2f;
            }
            else
            {
                // We're losing - offer concessions
                terms.payingFaction = proposer;
                terms.receivingFaction = target;
                terms.goldPayment = (theirStrength - ourStrength) * 1f;
            }

            return terms;
        }

        private bool ShouldProposeAlliance(FactionType us, FactionType them, AIPersonality personality)
        {
            var diplomacy = DiplomacySystem.Instance;

            // Already allied
            if (diplomacy.HasTreaty(us, them, TreatyType.Alliance))
                return false;

            // Check for common enemies
            bool hasCommonEnemy = false;
            var cm = CampaignManager.Instance;

            foreach (var otherFaction in cm.Factions.Keys)
            {
                if (otherFaction == us || otherFaction == them) continue;

                bool weAtWar = diplomacy.GetRelationState(us, otherFaction) == DiplomacyState.War;
                bool theyAtWar = diplomacy.GetRelationState(them, otherFaction) == DiplomacyState.War;

                if (weAtWar && theyAtWar)
                {
                    hasCommonEnemy = true;
                    break;
                }
            }

            if (hasCommonEnemy)
                return Random.value < 0.3f;

            // Diplomatic personality
            return Random.value < personality.diplomacyFocus * 0.05f;
        }

        private void ConsiderCoalition(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            var diplomacy = DiplomacySystem.Instance;

            // Find the dominant power
            FactionType? dominant = null;
            int maxStrength = 0;

            foreach (var f in cm.Factions.Keys)
            {
                if (cm.Factions[f].isEliminated) continue;

                int strength = CalculateMilitaryStrength(f);
                if (strength > maxStrength)
                {
                    maxStrength = strength;
                    dominant = f;
                }
            }

            if (!dominant.HasValue || dominant.Value == faction)
                return;

            // Check if dominant power is too strong
            int ourStrength = CalculateMilitaryStrength(faction);
            if (maxStrength < ourStrength * 2)
                return; // Not dominant enough

            // Try to form coalition
            foreach (var potentialAlly in cm.Factions.Keys)
            {
                if (potentialAlly == faction || potentialAlly == dominant.Value)
                    continue;
                if (cm.Factions[potentialAlly].isEliminated)
                    continue;

                // Check if they're also threatened
                float theirThreat = 0f;
                if (threatLevels.ContainsKey(potentialAlly) && 
                    threatLevels[potentialAlly].ContainsKey(dominant.Value))
                {
                    theirThreat = threatLevels[potentialAlly][dominant.Value];
                }

                if (theirThreat > 30f && Random.value < 0.2f)
                {
                    diplomacy.ProposeAlliance(faction, potentialAlly, AllianceType.Defensive);
                }
            }
        }

        #endregion

        #region Economy AI

        private void ProcessEconomy(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            var factionData = cm.Factions[faction];
            var personality = personalities[faction];

            // Build in cities
            foreach (var city in cm.Cities.Values)
            {
                if (city.owner != faction) continue;

                ProcessCityBuilding(city, factionData, personality);
            }
        }

        private void ProcessCityBuilding(CityData city, FactionData faction, AIPersonality personality)
        {
            // Skip if already building
            if (city.productionQueue.Count > 0) return;

            // Prioritize based on personality and needs
            float militaryNeed = personality.aggressiveness;
            float economyNeed = personality.economyFocus;

            // Check what we need
            if (faction.food < 100)
                economyNeed += 0.3f;
            if (faction.gold < 500)
                economyNeed += 0.2f;

            // Decide what to build
            if (economyNeed > militaryNeed)
            {
                // Build economic building
                if (!city.HasBuilding(BuildingType.Market))
                {
                    city.QueueBuilding(BuildingType.Market);
                }
                else if (!city.HasBuilding(BuildingType.Farm))
                {
                    city.QueueBuilding(BuildingType.Farm);
                }
            }
            else
            {
                // Build military building
                if (!city.HasBuilding(BuildingType.Barracks))
                {
                    city.QueueBuilding(BuildingType.Barracks);
                }
            }
        }

        #endregion

        #region Military AI

        private void ProcessMilitary(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            var personality = personalities[faction];

            // Get all armies
            var armies = new List<ArmyData>();
            foreach (var army in cm.Armies.Values)
            {
                if (army.faction == faction)
                    armies.Add(army);
            }

            foreach (var army in armies)
            {
                ProcessArmyOrders(army, faction, personality);
            }

            // Recruit if needed
            ProcessRecruitment(faction, personality);
        }

        private void ProcessArmyOrders(ArmyData army, FactionType faction, AIPersonality personality)
        {
            var cm = CampaignManager.Instance;
            var diplomacy = DiplomacySystem.Instance;

            if (army.movementPoints <= 0) return;

            // Find current province
            if (!cm.Provinces.TryGetValue(army.currentProvinceId, out var currentProvince))
                return;

            // Determine objective
            string targetProvinceId = null;
            float bestScore = float.MinValue;

            foreach (var neighborId in currentProvince.neighborIds)
            {
                if (!cm.Provinces.TryGetValue(neighborId, out var neighbor))
                    continue;

                float score = EvaluateProvinceTarget(army, neighbor, faction, personality);
                if (score > bestScore)
                {
                    bestScore = score;
                    targetProvinceId = neighborId;
                }
            }

            // Move if good target found
            if (targetProvinceId != null && bestScore > 0)
            {
                cm.MoveArmy(army.armyId, targetProvinceId);
            }
        }

        private float EvaluateProvinceTarget(ArmyData army, ProvinceData target, FactionType faction, AIPersonality personality)
        {
            var diplomacy = DiplomacySystem.Instance;
            float score = 0f;

            // Enemy province - attack potential
            if (target.owner != faction)
            {
                var state = diplomacy.GetRelationState(faction, target.owner);
                
                if (state == DiplomacyState.War)
                {
                    score += 50f * personality.aggressiveness;
                    
                    // Bonus for undefended provinces
                    var cm = CampaignManager.Instance;
                    var defendingArmies = cm.GetArmiesInProvince(target.provinceId);
                    if (defendingArmies.Count == 0)
                        score += 30f;
                }
                else
                {
                    // Can't attack non-enemies
                    score -= 100f;
                }
            }
            else
            {
                // Own province - defensive positioning
                // Move towards threatened borders
                foreach (var neighborId in target.neighborIds)
                {
                    var cm = CampaignManager.Instance;
                    if (cm.Provinces.TryGetValue(neighborId, out var neighbor))
                    {
                        if (neighbor.owner != faction)
                        {
                            var state = diplomacy.GetRelationState(faction, neighbor.owner);
                            if (state == DiplomacyState.War || state == DiplomacyState.Hostile)
                            {
                                score += 20f * (1f - personality.aggressiveness);
                            }
                        }
                    }
                }
            }

            return score;
        }

        private void ProcessRecruitment(FactionType faction, AIPersonality personality)
        {
            var cm = CampaignManager.Instance;
            var factionData = cm.Factions[faction];

            // Check if we need more troops
            int currentStrength = CalculateMilitaryStrength(faction);
            int desiredStrength = (int)(factionData.ownedProvinceIds.Count * 200 * personality.aggressiveness);

            if (currentStrength >= desiredStrength)
                return;

            // Find city with barracks
            foreach (var city in cm.Cities.Values)
            {
                if (city.owner != faction) continue;
                if (!city.HasBuilding(BuildingType.Barracks)) continue;
                if (city.productionQueue.Count > 0) continue;

                // Check if we can afford
                int cost = RegimentData.GetRecruitCostGold(UnitType.LineInfantry);
                if (factionData.gold >= cost)
                {
                    city.QueueUnit(UnitType.LineInfantry);
                    factionData.gold -= cost;
                    break;
                }
            }
        }

        #endregion

        #region Research AI

        private void ProcessResearch(FactionType faction)
        {
            var cm = CampaignManager.Instance;
            var factionData = cm.Factions[faction];
            var personality = personalities[faction];

            // Skip if already researching
            if (factionData.techTree.IsResearching())
                return;

            // Find best tech to research
            var availableTechs = factionData.techTree.GetAvailableTechnologies();
            if (availableTechs.Count == 0) return;

            Technology bestTech = null;
            float bestScore = float.MinValue;

            foreach (var tech in availableTechs)
            {
                float score = EvaluateTechnology(tech, personality);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTech = tech;
                }
            }

            if (bestTech != null)
            {
                factionData.techTree.StartResearch(bestTech.id);
            }
        }

        private float EvaluateTechnology(Technology tech, AIPersonality personality)
        {
            float score = 0f;

            switch (tech.category)
            {
                case TechCategory.Military:
                    score = personality.aggressiveness * 50f;
                    break;
                case TechCategory.Economy:
                    score = personality.economyFocus * 50f;
                    break;
                case TechCategory.Diplomacy:
                    score = personality.diplomacyFocus * 50f;
                    break;
            }

            // Prefer cheaper/faster techs
            score -= tech.researchCost * 0.01f;

            return score;
        }

        #endregion
    }

    /// <summary>
    /// AI personality traits for a faction
    /// </summary>
    [System.Serializable]
    public class AIPersonality
    {
        public float aggressiveness;    // 0-1, tendency to attack
        public float expansionism;      // 0-1, desire for territory
        public float diplomacyFocus;    // 0-1, preference for diplomacy
        public float economyFocus;      // 0-1, focus on economy
        public float navalFocus;        // 0-1, naval priority
        public List<FactionType> preferredTargets = new List<FactionType>();
    }
}
