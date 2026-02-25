using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.Campaign
{
    [System.Serializable]
    public class ArmyData
    {
        public string armyName;
        public string armyId;
        public FactionType faction;
        public string currentProvinceId;
        public string targetProvinceId;
        public bool isMoving;
        public int movementPoints = 2;
        public int maxMovementPoints = 2;
        
        // March movement
        public string originProvinceId;        // Province army is marching FROM
        public int marchDaysRemaining = 0;     // Days left to reach target
        public int marchDaysTotal = 0;         // Total days for march

        // Free-form movement (click anywhere on map)
        public Vector3 targetWorldPosition;    // Exact world position to move to
        public Vector3 originWorldPosition;    // World position where march started

        // === HOI4 ADDITIONS ===
        public float organization = 100f;      // 0-100, drops in combat, recovers at rest
        public float maxOrganization = 100f;
        public string generalId;               // Assigned general
        public string templateId;              // Division template used (optional)

        // === DEPTH MECHANICS ===
        public int turnsCampaigning = 0;       // Turns since last rest (fatigue tracker)
        public float fatigue = 0f;             // 0-1, increases while campaigning, reduces org recovery
        public bool isResting = false;         // True if army stayed in same province without orders

        // === SUPPLY / RATIONS ===
        public float currentRations = 15f;     // Days of food remaining
        public float maxRations = 30f;         // Max ration capacity
        public bool isStarving => currentRations <= 0f;
        public bool isLowOnFood => currentRations > 0f && currentRations <= 5f;

        public List<RegimentData> regiments = new List<RegimentData>();

        public int TotalSoldiers
        {
            get
            {
                int total = 0;
                foreach (var r in regiments) total += r.currentSize;
                return total;
            }
        }

        public float MaintenanceCost
        {
            get
            {
                float cost = 0f;
                foreach (var r in regiments) cost += r.maintenanceCostPerTurn;
                return cost;
            }
        }

        /// <summary>Get total equipment requirements for this army</summary>
        public Dictionary<EquipmentType, int> GetEquipmentNeeds()
        {
            var total = new Dictionary<EquipmentType, int>();
            foreach (var reg in regiments)
            {
                var needs = RegimentEquipmentNeeds.ForUnitType(reg.unitType, reg.currentSize);
                foreach (var kvp in needs.requirements)
                {
                    if (!total.ContainsKey(kvp.Key)) total[kvp.Key] = 0;
                    total[kvp.Key] += kvp.Value;
                }
            }
            return total;
        }

        public ArmyData(string name, string id, FactionType faction, string provinceId)
        {
            armyName = name;
            armyId = id;
            this.faction = faction;
            currentProvinceId = provinceId;
        }

        public void AddRegiment(RegimentData regiment)
        {
            regiments.Add(regiment);
        }

        public void RemoveRegiment(RegimentData regiment)
        {
            regiments.Remove(regiment);
        }

        public void ResetMovement()
        {
            movementPoints = maxMovementPoints;
            isMoving = false;
            targetProvinceId = null;
        }

        /// <summary>Recover organization when not in combat (per turn)</summary>
        public void RecoverOrganization(float infrastructureBonus = 1f)
        {
            float diffOrgRecovery = DifficultyManager.Instance.OrganizationRecoveryMultiplier;
            if (!isMoving)
            {
                float recovery = 10f * infrastructureBonus * diffOrgRecovery;
                recovery *= (1f - fatigue * 0.5f); // Fatigue reduces org recovery
                organization = Mathf.Min(maxOrganization, organization + recovery);
                isResting = true;
            }
            else
            {
                isResting = false;
            }
        }

        /// <summary>Process fatigue: increases while campaigning, decreases at rest</summary>
        public void ProcessFatigue()
        {
            float fatigueRate = DifficultyManager.Instance.ArmyFatigueRate;
            if (fatigueRate <= 0f) return; // Fatigue disabled at this difficulty

            if (isMoving || !isResting)
            {
                turnsCampaigning++;
                if (turnsCampaigning > 5)
                    fatigue = Mathf.Min(1f, fatigue + fatigueRate);
            }
            else
            {
                // Resting: reduce fatigue
                turnsCampaigning = Mathf.Max(0, turnsCampaigning - 2);
                fatigue = Mathf.Max(0f, fatigue - 0.05f);
            }
        }

        /// <summary>Apply winter attrition: lose soldiers if in the field during winter</summary>
        public void ApplyWinterAttrition(int currentTurn)
        {
            if (!DifficultyManager.IsWinterTurn(currentTurn)) return;

            float winterMult = DifficultyManager.Instance.WinterAttritionMultiplier;
            if (winterMult <= 0f) return;

            // Resting in a province = shelter, reduced attrition
            float shelterFactor = isResting ? 0.3f : 1.0f;

            foreach (var reg in regiments)
            {
                int losses = Mathf.Max(0, (int)(reg.currentSize * 0.02f * winterMult * shelterFactor));
                reg.currentSize = Mathf.Max(1, reg.currentSize - losses);
            }
        }

        // === SUPPLY & RATIONS ===

        /// <summary>
        /// Consume daily rations. Rates vary by army status.
        /// Returns true if army has food, false if starving.
        /// </summary>
        public bool ConsumeDailyRations(bool inFriendlyTerritory, bool isWinter)
        {
            float consumption;

            if (isMoving)
                consumption = 1.0f; // Full consumption when marching
            else if (inFriendlyTerritory)
                consumption = 0.3f; // Foraging in friendly territory
            else
                consumption = 0.7f; // Reduced foraging in enemy territory

            // Winter multiplier
            if (isWinter)
                consumption *= 1.5f;

            // Scale by army size (per 100 soldiers)
            float sizeScale = TotalSoldiers / 100f;
            consumption *= Mathf.Max(0.1f, sizeScale);

            currentRations = Mathf.Max(0f, currentRations - consumption);
            return currentRations > 0f;
        }

        /// <summary>Receive supply rations from a supply wagon.</summary>
        public void ReceiveSupply(float rations)
        {
            currentRations = Mathf.Min(maxRations, currentRations + rations);
        }

        /// <summary>
        /// Get combat penalty multiplier based on supply status.
        /// Returns (moraleMult, attackMult, defenseMult).
        /// </summary>
        public (float morale, float attack, float defense) GetSupplyPenalty()
        {
            if (currentRations >= 5f)
                return (1.0f, 1.0f, 1.0f);         // Well-fed
            else if (currentRations > 0f)
                return (0.8f, 0.9f, 0.9f);           // Low on food
            else
                return (0.5f, 0.7f, 0.6f);           // Starving
        }

        /// <summary>Apply starvation effects: attrition + fatigue.</summary>
        public void ApplyStarvationEffects()
        {
            if (!isStarving) return;

            // Attrition: lose 1-2% soldiers per day
            foreach (var reg in regiments)
            {
                int losses = Mathf.Max(0, Mathf.CeilToInt(reg.currentSize * Random.Range(0.01f, 0.02f)));
                reg.currentSize = Mathf.Max(1, reg.currentSize - losses);
            }

            // Fatigue surges
            fatigue = Mathf.Min(1f, fatigue + 0.15f);

            // Organization drops sharply
            organization = Mathf.Max(0f, organization - 10f);
        }
    }

    [System.Serializable]
    public class RegimentData
    {
        public string regimentName;
        public UnitType unitType;
        public int maxSize;
        public int currentSize;
        public float experience = 0f;
        public float maintenanceCostPerTurn;
        
        // === RANK SYSTEM ===
        public int rank = 0;                      // 0-9 (RegimentRank enum)
        public int battlesParticipated = 0;
        public int totalKills = 0;
        public bool promotionAvailable = false;
        public UnitType originalType;              // The type before any promotions
        
        // === REINFORCEMENT SYSTEM ===
        public bool isBeingReinforced = false;
        public int turnsToCompleteReinforcement = 0;
        public int targetReinforcementSize = 0;
        public int reinforcementCostGold = 0;
        public bool needsReinforcement => currentSize < maxSize && !isBeingReinforced;
        public float healthPercentage => maxSize > 0 ? (float)currentSize / maxSize : 1f;
        
        public RegimentData(string name, UnitType type, int size)
        {
            regimentName = name;
            unitType = type;
            originalType = type;
            maxSize = size;
            currentSize = size;

            maintenanceCostPerTurn = size * GetMaintenancePerSoldier(type);
        }

        public static int GetRecruitCostGold(UnitType type)
        {
            return type switch
            {
                // Infantry (cheapest → most expensive)
                UnitType.Militia => 50,
                UnitType.TrainedMilitia => 80,
                UnitType.LineInfantry => 150,
                UnitType.LightInfantry => 180,
                UnitType.Fusilier => 220,
                UnitType.Grenadier => 280,
                UnitType.Voltigeur => 320,
                UnitType.Chasseur => 380,
                UnitType.GuardInfantry => 500,
                UnitType.OldGuard => 700,
                // Cavalry
                UnitType.MilitiaCavalry => 120,
                UnitType.Dragoon => 250,
                UnitType.Cavalry => 300,
                UnitType.Hussar => 340,
                UnitType.Lancer => 360,
                UnitType.Cuirassier => 450,
                UnitType.GuardCavalry => 600,
                UnitType.Mameluke => 800,
                // Artillery
                UnitType.GarrisonCannon => 200,
                UnitType.Artillery => 400,
                UnitType.HorseArtillery => 500,
                UnitType.Howitzer => 650,
                UnitType.GrandBattery => 900,
                UnitType.GuardArtillery => 1200,
                // Special
                UnitType.Engineer => 300,
                UnitType.Sapper => 400,
                UnitType.Marine => 350,
                UnitType.Partisan => 100,
                _ => 100
            };
        }

        public static int GetRecruitTime(UnitType type)
        {
            return type switch
            {
                // Infantry
                UnitType.Militia => 1,
                UnitType.TrainedMilitia => 1,
                UnitType.LineInfantry => 2,
                UnitType.LightInfantry => 2,
                UnitType.Fusilier => 2,
                UnitType.Grenadier => 3,
                UnitType.Voltigeur => 3,
                UnitType.Chasseur => 3,
                UnitType.GuardInfantry => 4,
                UnitType.OldGuard => 5,
                // Cavalry
                UnitType.MilitiaCavalry => 1,
                UnitType.Dragoon => 2,
                UnitType.Cavalry => 2,
                UnitType.Hussar => 3,
                UnitType.Lancer => 3,
                UnitType.Cuirassier => 3,
                UnitType.GuardCavalry => 4,
                UnitType.Mameluke => 5,
                // Artillery
                UnitType.GarrisonCannon => 2,
                UnitType.Artillery => 3,
                UnitType.HorseArtillery => 3,
                UnitType.Howitzer => 4,
                UnitType.GrandBattery => 5,
                UnitType.GuardArtillery => 6,
                // Special
                UnitType.Engineer => 3,
                UnitType.Sapper => 3,
                UnitType.Marine => 3,
                UnitType.Partisan => 1,
                _ => 1
            };
        }

        /// <summary>Manpower cost to recruit this regiment type</summary>
        public static int GetManpowerCost(UnitType type)
        {
            return GetDefaultSize(type); // 1:1 manpower per soldier
        }

        public static int GetDefaultSize(UnitType type)
        {
            return type switch
            {
                // Infantry
                UnitType.Militia => 80,
                UnitType.TrainedMilitia => 70,
                UnitType.LineInfantry => 60,
                UnitType.LightInfantry => 40,
                UnitType.Fusilier => 50,
                UnitType.Grenadier => 40,
                UnitType.Voltigeur => 35,
                UnitType.Chasseur => 30,
                UnitType.GuardInfantry => 40,
                UnitType.OldGuard => 30,
                // Cavalry
                UnitType.MilitiaCavalry => 40,
                UnitType.Dragoon => 30,
                UnitType.Cavalry => 30,
                UnitType.Hussar => 25,
                UnitType.Lancer => 25,
                UnitType.Cuirassier => 20,
                UnitType.GuardCavalry => 20,
                UnitType.Mameluke => 15,
                // Artillery
                UnitType.GarrisonCannon => 4,
                UnitType.Artillery => 6,
                UnitType.HorseArtillery => 6,
                UnitType.Howitzer => 4,
                UnitType.GrandBattery => 12,
                UnitType.GuardArtillery => 8,
                // Special
                UnitType.Engineer => 30,
                UnitType.Sapper => 25,
                UnitType.Marine => 40,
                UnitType.Partisan => 50,
                _ => 60
            };
        }

        public static float GetMaintenancePerSoldier(UnitType type)
        {
            return type switch
            {
                // Infantry — cheapest
                UnitType.Militia or UnitType.TrainedMilitia => 0.5f,
                UnitType.LineInfantry or UnitType.LightInfantry => 1f,
                UnitType.Fusilier => 1.2f,
                UnitType.Grenadier => 1.5f,
                UnitType.Voltigeur or UnitType.Chasseur => 1.8f,
                UnitType.GuardInfantry => 2.5f,
                UnitType.OldGuard => 3.5f,
                // Cavalry — expensive
                UnitType.MilitiaCavalry => 1.5f,
                UnitType.Dragoon or UnitType.Cavalry => 2f,
                UnitType.Hussar or UnitType.Lancer => 2.5f,
                UnitType.Cuirassier => 3f,
                UnitType.GuardCavalry => 3.5f,
                UnitType.Mameluke => 4.5f,
                // Artillery — most expensive per man
                UnitType.GarrisonCannon => 2f,
                UnitType.Artillery => 3f,
                UnitType.HorseArtillery => 3.5f,
                UnitType.Howitzer => 4f,
                UnitType.GrandBattery => 4f,
                UnitType.GuardArtillery => 5f,
                // Special
                UnitType.Engineer or UnitType.Sapper => 2f,
                UnitType.Marine => 1.5f,
                UnitType.Partisan => 0.3f,
                _ => 1f
            };
        }

        // === RANK METHODS ===

        /// <summary>Recalculate rank from XP. Returns true if rank changed.</summary>
        public bool UpdateRank()
        {
            int oldRank = rank;
            rank = RegimentRankSystem.GetRankIndex(experience);
            
            // Check for promotion availability
            RegimentRank currentRank = (RegimentRank)rank;
            promotionAvailable = RegimentRankSystem.HasPromotionAvailable(unitType, currentRank);
            
            if (rank > oldRank)
            {
                string rankName = RegimentRankSystem.GetRankName(currentRank, unitType);
                Debug.Log($"[Regiment] {regimentName} promoted to Rank {rank} — {rankName}!");
                
                // Auto-promote unit type if applicable
                if (promotionAvailable)
                    ApplyPromotion();
                    
                return true;
            }
            return false;
        }

        /// <summary>Apply class promotion (e.g., LineInfantry → Grenadier).</summary>
        public void ApplyPromotion()
        {
            RegimentRank currentRank = (RegimentRank)rank;
            UnitType? newType = RegimentRankSystem.GetPromotionType(unitType, currentRank);
            if (newType == null) return;
            
            UnitType oldType = unitType;
            unitType = newType.Value;
            promotionAvailable = false;
            
            // Adjust maintenance for new type
            RecalculateMaintenance();
            
            Debug.Log($"[Regiment] {regimentName} CLASS PROMOTION: {oldType} → {unitType}!");
        }

        /// <summary>Process battle results: add XP, update kills/battles, check rank up.</summary>
        public void ProcessBattleResult(bool won, int casualties, int kills)
        {
            battlesParticipated++;
            totalKills += kills;
            
            int initialSize = currentSize + casualties; // Size before battle
            float xpGain = RegimentRankSystem.CalculateBattleXP(won, initialSize, casualties, kills);
            experience = Mathf.Min(experience + xpGain, 100f);
            
            // Apply casualties
            currentSize = Mathf.Max(0, currentSize - casualties);
            
            // Check rank up
            UpdateRank();
        }

        /// <summary>Recalculate maintenance cost based on current unit type and size.</summary>  
        public void RecalculateMaintenance()
        {
            maintenanceCostPerTurn = currentSize * GetMaintenancePerSoldier(unitType);
        }

        /// <summary>Get display rank name.</summary>
        public string RankDisplayName => RegimentRankSystem.GetRankName((RegimentRank)rank, unitType);

        /// <summary>Get rank color for UI.</summary>
        public Color RankColor => RegimentRankSystem.GetRankColor((RegimentRank)rank);
    }
}
