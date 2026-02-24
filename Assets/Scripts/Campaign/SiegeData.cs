using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Data structures for the siege system on the campaign map.
    /// </summary>
    
    public enum SiegeState
    {
        None,
        Besieging,      // Army is surrounding the city
        Assaulting,     // Active assault in progress
        Negotiating,    // Peace talks
        Lifted          // Siege was lifted (defender won)
    }

    public enum SiegeEquipmentType
    {
        Ladder,         // For scaling walls
        Ram,            // For breaking gates
        SiegeTower,     // Mobile tower for wall assault
        SiegeCannon,    // Heavy artillery for breaching walls
        Sapper,         // Engineers for undermining walls
        Mortar          // For bombardment over walls
    }

    /// <summary>
    /// Represents an active siege on the campaign map
    /// </summary>
    [System.Serializable]
    public class SiegeData
    {
        public string siegeId;
        public string cityId;
        public string attackerArmyId;
        public string defenderArmyId; // Garrison or relief army
        public FactionType attackerFaction;
        public FactionType defenderFaction;
        
        public SiegeState state = SiegeState.Besieging;
        public int turnStarted;
        public int turnsElapsed;
        
        // Siege equipment
        public Dictionary<SiegeEquipmentType, int> equipment = new Dictionary<SiegeEquipmentType, int>();
        
        // Attrition tracking
        public float attackerAttrition;  // Cumulative losses from disease/starvation
        public float defenderAttrition;
        public float defenderFoodSupply = 100f; // Days of food remaining
        public float defenderMorale = 100f;
        
        // Fortification damage
        public float wallDamage;         // 0-100, affects defense
        public float gateDamage;         // 0-100, gate can be destroyed at 100
        public int breachCount;          // Number of breaches in walls
        
        public SiegeData(string cityId, string attackerArmyId, FactionType attacker, FactionType defender)
        {
            this.siegeId = System.Guid.NewGuid().ToString();
            this.cityId = cityId;
            this.attackerArmyId = attackerArmyId;
            this.attackerFaction = attacker;
            this.defenderFaction = defender;
            this.turnStarted = CampaignManager.Instance?.CurrentTurn ?? 0;
            
            // Initialize with basic equipment
            equipment[SiegeEquipmentType.Ladder] = 0;
            equipment[SiegeEquipmentType.Ram] = 0;
            equipment[SiegeEquipmentType.SiegeCannon] = 0;
        }

        public bool CanAssault()
        {
            // Can assault if have ladders, or gate is destroyed, or have breaches
            return equipment.GetValueOrDefault(SiegeEquipmentType.Ladder, 0) > 0 ||
                   gateDamage >= 100f ||
                   breachCount > 0;
        }

        public float GetAssaultDifficultyModifier()
        {
            float modifier = 1f;
            
            // Breaches make assault easier
            modifier -= breachCount * 0.15f;
            
            // Destroyed gate makes assault easier
            if (gateDamage >= 100f)
                modifier -= 0.3f;
            
            // Wall damage reduces defense
            modifier -= (wallDamage / 100f) * 0.2f;
            
            // Low defender morale
            modifier -= (100f - defenderMorale) / 100f * 0.2f;
            
            return Mathf.Clamp(modifier, 0.3f, 1.5f);
        }
    }

    /// <summary>
    /// Fortification data for a city
    /// </summary>
    [System.Serializable]
    public class FortificationData
    {
        public int level = 1;           // 1-5, affects wall HP and towers
        public float wallHP = 100f;
        public float maxWallHP = 100f;
        public float gateHP = 100f;
        public float maxGateHP = 100f;
        public int towerCount = 4;      // Number of defensive towers
        public bool hasMoat = false;
        public bool hasBastion = false;
        
        // Calculated stats based on level
        public int GarrisonCapacity => level * 500;
        public float DefenseBonus => level * 0.1f; // 10% per level
        public int TowerDamage => 10 + level * 5;  // Damage per tower per turn
        
        public FortificationData(int level = 1)
        {
            SetLevel(level);
        }

        public void SetLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, 5);
            
            switch (level)
            {
                case 1: // Wooden palisade
                    maxWallHP = 100f;
                    maxGateHP = 80f;
                    towerCount = 2;
                    hasMoat = false;
                    hasBastion = false;
                    break;
                case 2: // Stone walls
                    maxWallHP = 200f;
                    maxGateHP = 150f;
                    towerCount = 4;
                    hasMoat = false;
                    hasBastion = false;
                    break;
                case 3: // Fortified walls with moat
                    maxWallHP = 350f;
                    maxGateHP = 250f;
                    towerCount = 6;
                    hasMoat = true;
                    hasBastion = false;
                    break;
                case 4: // Star fortress
                    maxWallHP = 500f;
                    maxGateHP = 400f;
                    towerCount = 8;
                    hasMoat = true;
                    hasBastion = true;
                    break;
                case 5: // Citadel
                    maxWallHP = 750f;
                    maxGateHP = 600f;
                    towerCount = 12;
                    hasMoat = true;
                    hasBastion = true;
                    break;
            }
            
            wallHP = maxWallHP;
            gateHP = maxGateHP;
        }

        public void TakeDamage(float wallDamage, float gateDamage)
        {
            wallHP = Mathf.Max(0, wallHP - wallDamage);
            gateHP = Mathf.Max(0, gateHP - gateDamage);
        }

        public void Repair(float amount)
        {
            wallHP = Mathf.Min(maxWallHP, wallHP + amount);
            gateHP = Mathf.Min(maxGateHP, gateHP + amount * 0.5f);
        }

        public bool IsGateDestroyed => gateHP <= 0;
        public bool HasBreach => wallHP < maxWallHP * 0.3f;
        public float WallIntegrity => wallHP / maxWallHP;
        public float GateIntegrity => gateHP / maxGateHP;
    }

    /// <summary>
    /// Siege equipment that can be built
    /// </summary>
    [System.Serializable]
    public class SiegeEquipment
    {
        public SiegeEquipmentType type;
        public string name;
        public int goldCost;
        public int turnsToBuild;
        public string description;
        
        // Combat stats
        public float wallDamagePerTurn;
        public float gateDamagePerTurn;
        public int assaultBonus;        // Bonus troops can use this for assault
        
        public static SiegeEquipment GetEquipmentData(SiegeEquipmentType type)
        {
            switch (type)
            {
                case SiegeEquipmentType.Ladder:
                    return new SiegeEquipment
                    {
                        type = type,
                        name = "Siege Ladders",
                        goldCost = 50,
                        turnsToBuild = 1,
                        description = "Basic scaling equipment. Allows infantry to assault walls.",
                        assaultBonus = 20
                    };
                    
                case SiegeEquipmentType.Ram:
                    return new SiegeEquipment
                    {
                        type = type,
                        name = "Battering Ram",
                        goldCost = 150,
                        turnsToBuild = 2,
                        description = "Heavy ram for breaking gates.",
                        gateDamagePerTurn = 25f
                    };
                    
                case SiegeEquipmentType.SiegeTower:
                    return new SiegeEquipment
                    {
                        type = type,
                        name = "Siege Tower",
                        goldCost = 300,
                        turnsToBuild = 3,
                        description = "Mobile tower allowing protected wall assault.",
                        assaultBonus = 50
                    };
                    
                case SiegeEquipmentType.SiegeCannon:
                    return new SiegeEquipment
                    {
                        type = type,
                        name = "Siege Cannon",
                        goldCost = 500,
                        turnsToBuild = 2,
                        description = "Heavy artillery for breaching walls.",
                        wallDamagePerTurn = 30f,
                        gateDamagePerTurn = 15f
                    };
                    
                case SiegeEquipmentType.Sapper:
                    return new SiegeEquipment
                    {
                        type = type,
                        name = "Sapper Team",
                        goldCost = 200,
                        turnsToBuild = 1,
                        description = "Engineers who undermine walls, creating breaches.",
                        wallDamagePerTurn = 20f
                    };
                    
                case SiegeEquipmentType.Mortar:
                    return new SiegeEquipment
                    {
                        type = type,
                        name = "Siege Mortar",
                        goldCost = 400,
                        turnsToBuild = 2,
                        description = "Lobs explosives over walls, damaging garrison and morale.",
                        wallDamagePerTurn = 10f
                    };
                    
                default:
                    return null;
            }
        }
    }
}
