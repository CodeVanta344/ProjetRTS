using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Manages siege operations on the campaign map.
    /// Handles siege initiation, attrition, equipment building, and assault resolution.
    /// </summary>
    public class SiegeManager
    {
        private static SiegeManager _instance;
        public static SiegeManager Instance => _instance ??= new SiegeManager();

        // Active sieges
        private Dictionary<string, SiegeData> activeSieges = new Dictionary<string, SiegeData>();
        
        // Equipment being built
        private List<EquipmentConstruction> constructionQueue = new List<EquipmentConstruction>();

        // Events
        public delegate void SiegeEvent(SiegeData siege);
        public event SiegeEvent OnSiegeStarted;
        public event SiegeEvent OnSiegeEnded;
        public event SiegeEvent OnAssaultStarted;
        public event SiegeEvent OnBreachCreated;

        public Dictionary<string, SiegeData> ActiveSieges => activeSieges;

        #region Siege Initiation

        /// <summary>
        /// Start a siege when an army attacks a fortified city
        /// </summary>
        public SiegeData StartSiege(string cityId, string attackerArmyId)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return null;

            // Get city and army data
            if (!cm.Cities.TryGetValue(cityId, out CityData city)) return null;
            if (!cm.Armies.TryGetValue(attackerArmyId, out ArmyData attackerArmy)) return null;

            // Check if city is already under siege
            if (IsCityUnderSiege(cityId))
            {
                Debug.Log($"[Siege] {city.cityName} is already under siege!");
                return null;
            }

            // Create siege data
            var siege = new SiegeData(cityId, attackerArmyId, attackerArmy.faction, city.owner);
            
            // Find defender army (garrison or army in city)
            var defendingArmies = cm.GetArmiesInProvince(city.provinceId);
            foreach (var army in defendingArmies)
            {
                if (army.faction == city.owner)
                {
                    siege.defenderArmyId = army.armyId;
                    break;
                }
            }

            // Calculate initial food supply based on city population and garrison
            siege.defenderFoodSupply = CalculateInitialFoodSupply(city);

            activeSieges[siege.siegeId] = siege;
            
            OnSiegeStarted?.Invoke(siege);
            Debug.Log($"[Siege] {attackerArmy.faction} begins siege of {city.cityName}!");

            return siege;
        }

        /// <summary>
        /// Lift a siege (attacker retreats or is defeated)
        /// </summary>
        public void LiftSiege(string siegeId, bool attackerVictory = false)
        {
            if (!activeSieges.TryGetValue(siegeId, out SiegeData siege))
                return;

            siege.state = attackerVictory ? SiegeState.None : SiegeState.Lifted;
            
            OnSiegeEnded?.Invoke(siege);
            activeSieges.Remove(siegeId);

            var cm = CampaignManager.Instance;
            if (cm != null && cm.Cities.TryGetValue(siege.cityId, out CityData city))
            {
                Debug.Log($"[Siege] Siege of {city.cityName} has ended. " +
                    (attackerVictory ? "City captured!" : "Siege lifted!"));
            }
        }

        public bool IsCityUnderSiege(string cityId)
        {
            foreach (var siege in activeSieges.Values)
            {
                if (siege.cityId == cityId && siege.state == SiegeState.Besieging)
                    return true;
            }
            return false;
        }

        public SiegeData GetSiegeForCity(string cityId)
        {
            foreach (var siege in activeSieges.Values)
            {
                if (siege.cityId == cityId)
                    return siege;
            }
            return null;
        }

        #endregion

        #region Turn Processing

        /// <summary>
        /// Process all active sieges at end of turn
        /// </summary>
        public void ProcessTurn()
        {
            var siegesToEnd = new List<string>();

            foreach (var siege in activeSieges.Values)
            {
                if (siege.state != SiegeState.Besieging)
                    continue;

                siege.turnsElapsed++;

                // Process attrition
                ProcessAttrition(siege);

                // Process equipment damage to fortifications
                ProcessBombardment(siege);

                // Check for automatic surrender
                if (CheckSurrender(siege))
                {
                    siegesToEnd.Add(siege.siegeId);
                    continue;
                }

                // Process equipment construction
                ProcessConstruction(siege);
            }

            // End sieges that resulted in surrender
            foreach (var siegeId in siegesToEnd)
            {
                ResolveSurrender(siegeId);
            }
        }

        private void ProcessAttrition(SiegeData siege)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            // Defender attrition (starvation, disease)
            siege.defenderFoodSupply -= 10f; // Lose 10 days of food per turn
            
            if (siege.defenderFoodSupply <= 0)
            {
                // Starvation attrition
                siege.defenderAttrition += 5f;
                siege.defenderMorale -= 15f;
                
                // Apply casualties to garrison
                if (cm.Cities.TryGetValue(siege.cityId, out CityData city))
                {
                    city.garrisonSize = Mathf.Max(0, city.garrisonSize - (int)(city.garrisonSize * 0.05f));
                }
            }
            else if (siege.defenderFoodSupply < 30f)
            {
                // Low food morale penalty
                siege.defenderMorale -= 5f;
            }

            // Attacker attrition (disease from camp)
            siege.attackerAttrition += 2f; // Base attrition
            
            // Apply attacker casualties
            if (cm.Armies.TryGetValue(siege.attackerArmyId, out ArmyData attackerArmy))
            {
                foreach (var regiment in attackerArmy.regiments)
                {
                    int losses = Mathf.RoundToInt(regiment.currentSize * 0.02f);
                    regiment.currentSize = Mathf.Max(1, regiment.currentSize - losses);
                }
            }

            siege.defenderMorale = Mathf.Clamp(siege.defenderMorale, 0f, 100f);
        }

        private void ProcessBombardment(SiegeData siege)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            if (!cm.Cities.TryGetValue(siege.cityId, out CityData city))
                return;

            // Get city fortifications
            int fortLevel = city.GetFortificationLevel();
            if (fortLevel <= 0) return;

            float totalWallDamage = 0f;
            float totalGateDamage = 0f;

            // Calculate damage from siege equipment
            foreach (var kvp in siege.equipment)
            {
                var equipData = SiegeEquipment.GetEquipmentData(kvp.Key);
                if (equipData == null) continue;

                int count = kvp.Value;
                totalWallDamage += equipData.wallDamagePerTurn * count;
                totalGateDamage += equipData.gateDamagePerTurn * count;
            }

            // Apply damage based on fortification level
            float fortDefense = fortLevel * 10f; // Higher level = more resistance
            
            if (totalWallDamage > 0 || totalGateDamage > 0)
            {
                // Apply damage with defense reduction
                float effectiveWallDamage = Mathf.Max(0, totalWallDamage - fortDefense * 0.5f);
                float effectiveGateDamage = Mathf.Max(0, totalGateDamage - fortDefense * 0.3f);
                
                siege.wallDamage += effectiveWallDamage;
                siege.gateDamage += effectiveGateDamage;

                // Check for breach (wall damage > 50%)
                if (siege.wallDamage >= 50f && siege.breachCount == 0)
                {
                    siege.breachCount = 1;
                    OnBreachCreated?.Invoke(siege);
                    Debug.Log($"[Siege] Breach created in {city.cityName}'s walls!");
                }

                // Check for gate destruction (gate damage > 100%)
                if (siege.gateDamage >= 100f)
                {
                    siege.gateDamage = 100f;
                    Debug.Log($"[Siege] Gates of {city.cityName} have been destroyed!");
                }
            }
        }

        private bool CheckSurrender(SiegeData siege)
        {
            // Surrender conditions:
            // 1. Morale drops to 0
            // 2. No food for 5+ turns
            // 3. Garrison wiped out

            if (siege.defenderMorale <= 0)
                return true;

            if (siege.defenderFoodSupply <= -50f) // 5 turns without food
                return true;

            var cm = CampaignManager.Instance;
            if (cm != null && cm.Cities.TryGetValue(siege.cityId, out CityData city))
            {
                if (city.garrisonSize <= 0)
                    return true;
            }

            return false;
        }

        private void ResolveSurrender(string siegeId)
        {
            if (!activeSieges.TryGetValue(siegeId, out SiegeData siege))
                return;

            var cm = CampaignManager.Instance;
            if (cm == null) return;

            // Transfer city to attacker
            if (cm.Cities.TryGetValue(siege.cityId, out CityData city))
            {
                cm.TransferCity(siege.cityId, siege.attackerFaction);
                Debug.Log($"[Siege] {city.cityName} has surrendered to {siege.attackerFaction}!");
            }

            LiftSiege(siegeId, true);
        }

        #endregion

        #region Equipment Construction

        public bool CanBuildEquipment(SiegeData siege, SiegeEquipmentType type)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return false;

            var equipData = SiegeEquipment.GetEquipmentData(type);
            if (equipData == null) return false;

            // Check gold
            if (!cm.Factions[siege.attackerFaction].CanAfford(equipData.goldCost))
                return false;

            // Check if already building this type
            foreach (var construction in constructionQueue)
            {
                if (construction.siegeId == siege.siegeId && construction.type == type)
                    return false;
            }

            return true;
        }

        public void StartBuildingEquipment(SiegeData siege, SiegeEquipmentType type)
        {
            if (!CanBuildEquipment(siege, type))
                return;

            var cm = CampaignManager.Instance;
            var equipData = SiegeEquipment.GetEquipmentData(type);

            // Pay cost
            cm.Factions[siege.attackerFaction].Spend(equipData.goldCost);

            // Add to construction queue
            constructionQueue.Add(new EquipmentConstruction
            {
                siegeId = siege.siegeId,
                type = type,
                turnsRemaining = equipData.turnsToBuild
            });

            Debug.Log($"[Siege] Started building {equipData.name} ({equipData.turnsToBuild} turns)");
        }

        private void ProcessConstruction(SiegeData siege)
        {
            var completed = new List<EquipmentConstruction>();

            foreach (var construction in constructionQueue)
            {
                if (construction.siegeId != siege.siegeId)
                    continue;

                construction.turnsRemaining--;

                if (construction.turnsRemaining <= 0)
                {
                    completed.Add(construction);
                }
            }

            // Complete constructions
            foreach (var construction in completed)
            {
                if (!siege.equipment.ContainsKey(construction.type))
                    siege.equipment[construction.type] = 0;
                
                siege.equipment[construction.type]++;
                constructionQueue.Remove(construction);

                var equipData = SiegeEquipment.GetEquipmentData(construction.type);
                Debug.Log($"[Siege] {equipData.name} completed!");
            }
        }

        #endregion

        #region Assault

        /// <summary>
        /// Check if assault is possible
        /// </summary>
        public bool CanAssault(SiegeData siege)
        {
            if (siege.state != SiegeState.Besieging)
                return false;

            return siege.CanAssault();
        }

        /// <summary>
        /// Launch an assault on the city - triggers a siege battle
        /// </summary>
        public void LaunchAssault(SiegeData siege)
        {
            if (!CanAssault(siege))
            {
                Debug.Log("[Siege] Cannot assault - need ladders, siege towers, or a breach!");
                return;
            }

            siege.state = SiegeState.Assaulting;
            OnAssaultStarted?.Invoke(siege);

            // Transition to siege battle scene
            var cm = CampaignManager.Instance;
            if (cm != null && cm.Cities.TryGetValue(siege.cityId, out CityData city))
            {
                Debug.Log($"[Siege] Assault on {city.cityName} begins!");
                
                // Store siege data for battle
                if (NapoleonicWars.Core.GameManager.Instance != null)
                {
                    NapoleonicWars.Core.GameManager.Instance.AttackerArmyId = siege.attackerArmyId;
                    NapoleonicWars.Core.GameManager.Instance.DefenderArmyId = siege.defenderArmyId;
                    NapoleonicWars.Core.GameManager.Instance.BattleProvinceId = city.provinceId;
                    
                    // Load siege battle scene
                    NapoleonicWars.UI.LoadingScreenUI.LoadSceneWithScreen("SiegeBattle");
                }
            }
        }

        /// <summary>
        /// Resolve assault result after battle
        /// </summary>
        public void ResolveAssault(string siegeId, bool attackerVictory)
        {
            if (!activeSieges.TryGetValue(siegeId, out SiegeData siege))
                return;

            if (attackerVictory)
            {
                // City captured
                var cm = CampaignManager.Instance;
                if (cm != null)
                {
                    cm.TransferCity(siege.cityId, siege.attackerFaction);
                }
                LiftSiege(siegeId, true);
            }
            else
            {
                // Assault failed, continue siege
                siege.state = SiegeState.Besieging;
                siege.defenderMorale = Mathf.Min(100f, siege.defenderMorale + 20f); // Morale boost for defenders
                
                Debug.Log("[Siege] Assault repelled! Siege continues...");
            }
        }

        #endregion

        #region Negotiation

        /// <summary>
        /// Offer terms to the defender
        /// </summary>
        public void OfferTerms(SiegeData siege, SiegeTerms terms)
        {
            // AI evaluation of terms
            float acceptChance = EvaluateTerms(siege, terms);
            
            if (Random.value < acceptChance)
            {
                AcceptTerms(siege, terms);
            }
            else
            {
                Debug.Log("[Siege] Defender rejected the terms!");
            }
        }

        private float EvaluateTerms(SiegeData siege, SiegeTerms terms)
        {
            float acceptChance = 0f;

            // Base chance from morale
            acceptChance += (100f - siege.defenderMorale) / 100f * 0.3f;

            // Food situation
            if (siege.defenderFoodSupply <= 0)
                acceptChance += 0.3f;
            else if (siege.defenderFoodSupply < 30f)
                acceptChance += 0.15f;

            // Wall damage
            acceptChance += (siege.wallDamage / 100f) * 0.2f;

            // Terms generosity
            if (terms.allowGarrisonToLeave)
                acceptChance += 0.2f;
            if (!terms.sackCity)
                acceptChance += 0.1f;

            return Mathf.Clamp01(acceptChance);
        }

        private void AcceptTerms(SiegeData siege, SiegeTerms terms)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            // Transfer city
            cm.TransferCity(siege.cityId, siege.attackerFaction);

            // Handle garrison
            if (terms.allowGarrisonToLeave && !string.IsNullOrEmpty(siege.defenderArmyId))
            {
                // Move defender army to nearest friendly province
                // (simplified - just leave them in place for now)
            }

            // Handle sacking
            if (terms.sackCity && cm.Cities.TryGetValue(siege.cityId, out CityData city))
            {
                // Gain gold, damage city
                float lootGold = city.population * 0.1f;
                cm.Factions[siege.attackerFaction].gold += lootGold;
                // Reduce all population classes by 30% (sacking effects)
                city.populationData.workers = (int)(city.populationData.workers * 0.7f);
                city.populationData.bourgeois = (int)(city.populationData.bourgeois * 0.7f);
                city.populationData.nobles = (int)(city.populationData.nobles * 0.7f);
                city.publicOrder -= 30f;
                
                Debug.Log($"[Siege] City sacked! Gained {lootGold:F0} gold.");
            }

            LiftSiege(siege.siegeId, true);
            Debug.Log("[Siege] Defender accepted terms. City surrendered peacefully.");
        }

        #endregion

        #region Helpers

        private float CalculateInitialFoodSupply(CityData city)
        {
            // Base supply from stored food
            float supply = city.storedFood;
            
            // Add from population (they have some reserves)
            supply += city.population * 0.01f;
            
            // Cap at 200 days
            return Mathf.Min(200f, supply);
        }

        #endregion
    }

    /// <summary>
    /// Equipment under construction
    /// </summary>
    [System.Serializable]
    public class EquipmentConstruction
    {
        public string siegeId;
        public SiegeEquipmentType type;
        public int turnsRemaining;
    }

    /// <summary>
    /// Terms offered for surrender
    /// </summary>
    [System.Serializable]
    public class SiegeTerms
    {
        public bool allowGarrisonToLeave = true;
        public bool sackCity = false;
        public float goldDemand = 0f;
    }
}
