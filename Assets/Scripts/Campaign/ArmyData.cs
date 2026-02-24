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

        // === HOI4 ADDITIONS ===
        public float organization = 100f;      // 0-100, drops in combat, recovers at rest
        public float maxOrganization = 100f;
        public string generalId;               // Assigned general
        public string templateId;              // Division template used (optional)

        // === DEPTH MECHANICS ===
        public int turnsCampaigning = 0;       // Turns since last rest (fatigue tracker)
        public float fatigue = 0f;             // 0-1, increases while campaigning, reduces org recovery
        public bool isResting = false;         // True if army stayed in same province without orders

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

            switch (type)
            {
                case UnitType.LineInfantry:
                case UnitType.LightInfantry:
                    maintenanceCostPerTurn = size * 1f;
                    break;
                case UnitType.Grenadier:
                    maintenanceCostPerTurn = size * 1.5f;
                    break;
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    maintenanceCostPerTurn = size * 2f;
                    break;
                case UnitType.Artillery:
                    maintenanceCostPerTurn = size * 3f;
                    break;
            }
        }

        public static int GetRecruitCostGold(UnitType type)
        {
            switch (type)
            {
                case UnitType.LineInfantry: return 150;
                case UnitType.LightInfantry: return 180;
                case UnitType.Grenadier: return 250;
                case UnitType.Cavalry: return 300;
                case UnitType.Hussar: return 320;
                case UnitType.Lancer: return 310;
                case UnitType.Artillery: return 500;
                default: return 100;
            }
        }

        public static int GetRecruitTime(UnitType type)
        {
            switch (type)
            {
                case UnitType.LineInfantry: return 1;
                case UnitType.LightInfantry: return 1;
                case UnitType.Grenadier: return 2;
                case UnitType.Cavalry: return 2;
                case UnitType.Hussar: return 2;
                case UnitType.Lancer: return 2;
                case UnitType.Artillery: return 3;
                default: return 1;
            }
        }

        /// <summary>Manpower cost to recruit this regiment type</summary>
        public static int GetManpowerCost(UnitType type)
        {
            return GetDefaultSize(type); // 1:1 manpower per soldier
        }

        public static int GetDefaultSize(UnitType type)
        {
            switch (type)
            {
                case UnitType.LineInfantry: return 60;
                case UnitType.LightInfantry: return 40;
                case UnitType.Grenadier: return 40;
                case UnitType.Cavalry: return 30;
                case UnitType.Hussar: return 30;
                case UnitType.Lancer: return 30;
                case UnitType.Artillery: return 6;
                case UnitType.ImperialGuard: return 40;
                case UnitType.GuardCavalry: return 25;
                case UnitType.GuardArtillery: return 6;
                default: return 60;
            }
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
            float costPerSoldier = unitType switch
            {
                UnitType.LineInfantry or UnitType.LightInfantry => 1f,
                UnitType.Grenadier => 1.5f,
                UnitType.Cavalry or UnitType.Hussar or UnitType.Lancer => 2f,
                UnitType.Artillery => 3f,
                UnitType.ImperialGuard => 2.5f,
                UnitType.GuardCavalry => 3.5f,
                UnitType.GuardArtillery => 4f,
                _ => 1f
            };
            maintenanceCostPerTurn = currentSize * costPerSoldier;
        }

        /// <summary>Get display rank name.</summary>
        public string RankDisplayName => RegimentRankSystem.GetRankName((RegimentRank)rank, unitType);

        /// <summary>Get rank color for UI.</summary>
        public Color RankColor => RegimentRankSystem.GetRankColor((RegimentRank)rank);
    }
}
