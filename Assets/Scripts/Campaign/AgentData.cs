using System;
using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Types of intelligence agents
    /// </summary>
    public enum AgentType
    {
        Spy,        // Classic spy - gathers intel, can establish networks
        Diplomat,   // Negotiates, improves relations, can bribe
        Assassin,   // Kills enemy agents/generals
        Saboteur    // Specialized in pre-battle sabotage
    }

    /// <summary>
    /// Agent states during their missions
    /// </summary>
    public enum AgentState
    {
        Idle,           // Waiting for orders
        Moving,         // Traveling to destination
        Infiltrating,   // Entering enemy territory/city
        GatheringIntel, // Actively collecting information
        EstablishingNetwork, // Setting up spy network (takes multiple turns)
        Sabotaging,     // Performing sabotage before battle
        Hiding,         // Laying low after failed action
        Captured,       // Caught by enemy
        Dead            // Killed in action
    }

    /// <summary>
    /// Represents an intelligence agent on the campaign map.
    /// Agents operate independently, can move further than armies,
    /// and gather intelligence in enemy territories.
    /// </summary>
    [Serializable]
    public class AgentData
    {
        public string agentId;
        public string agentName;
        public AgentType agentType;
        public FactionType faction;
        public AgentState currentState;
        
        // Position
        public string currentProvinceId;
        public Vector2 mapPosition; // Normalized 0-1 coordinates
        
        // Movement - agents can move 2x further than armies
        public int movementPoints;
        public int maxMovementPoints = 20; // Double army movement
        
        // Agent stats
        public int skill;           // 1-10, affects success chance
        public int experience;      // Gained over time, improves skill
        public int subtlety;        // 1-10, affects detection avoidance
        public int disguiseQuality; // 1-10, affects infiltration success
        
        // Mission tracking
        public string targetProvinceId;
        public string targetArmyId;
        public int turnsInLocation;
        public int missionProgress;
        public int missionTargetTurns;
        
        // Network establishment
        public bool hasEstablishedNetwork;
        public string networkProvinceId;
        public int networkLevel; // 1-3, higher = more intel/better sabotage
        
        // Sabotage capabilities (when network established)
        public bool canSabotageGates;
        public bool canPoisonWells;
        public bool canInciteRebellion;
        public bool canAssassinateGeneral;
        
        // Risk tracking
        public int suspicionLevel;    // 0-100, higher = more likely to be caught
        public bool isCompromised;    // If true, easier to detect
        public int turnsUntilSafe;    // Countdown after failed action
        
        // Intel gathered
        public List<IntelReport> gatheredIntel = new List<IntelReport>();
        
        public AgentData(string id, string name, AgentType type, FactionType faction)
        {
            this.agentId = id;
            this.agentName = name;
            this.agentType = type;
            this.faction = faction;
            this.currentState = AgentState.Idle;
            this.movementPoints = maxMovementPoints;
            
            // Initialize stats based on type
            InitializeStats(type);
        }
        
        private void InitializeStats(AgentType type)
        {
            switch (type)
            {
                case AgentType.Spy:
                    skill = UnityEngine.Random.Range(3, 7);
                    subtlety = UnityEngine.Random.Range(4, 8);
                    disguiseQuality = UnityEngine.Random.Range(3, 7);
                    break;
                case AgentType.Diplomat:
                    skill = UnityEngine.Random.Range(4, 8);
                    subtlety = UnityEngine.Random.Range(3, 6);
                    disguiseQuality = UnityEngine.Random.Range(5, 9);
                    break;
                case AgentType.Assassin:
                    skill = UnityEngine.Random.Range(5, 9);
                    subtlety = UnityEngine.Random.Range(5, 10);
                    disguiseQuality = UnityEngine.Random.Range(4, 8);
                    break;
                case AgentType.Saboteur:
                    skill = UnityEngine.Random.Range(4, 8);
                    subtlety = UnityEngine.Random.Range(3, 7);
                    disguiseQuality = UnityEngine.Random.Range(3, 6);
                    break;
            }
            
            experience = 0;
            suspicionLevel = 0;
            isCompromised = false;
            hasEstablishedNetwork = false;
        }
        
        /// <summary>
        /// Check if agent can move to a province
        /// </summary>
        public bool CanMoveTo(int distance)
        {
            return movementPoints >= distance;
        }
        
        /// <summary>
        /// Calculate detection chance based on current situation
        /// </summary>
        public float GetDetectionChance(bool isEnemyTerritory, bool hasEnemyGarrison)
        {
            float baseChance = 5f; // 5% base detection per turn
            
            // Modifiers
            if (isEnemyTerritory) baseChance += 10f;
            if (hasEnemyGarrison) baseChance += 15f;
            if (isCompromised) baseChance += 25f;
            if (hasEstablishedNetwork) baseChance -= 10f; // Network provides cover
            
            // Agent abilities reduce detection
            baseChance -= subtlety * 2f;
            baseChance -= disguiseQuality * 1f;
            
            // Experience helps
            baseChance -= (experience / 50f) * 5f;
            
            return Mathf.Clamp(baseChance, 1f, 80f); // Never 0% or 100%
        }
        
        /// <summary>
        /// Calculate success chance for establishing a network
        /// </summary>
        public float GetNetworkEstablishChance()
        {
            float baseChance = 30f;
            baseChance += skill * 5f;
            baseChance += subtlety * 3f;
            baseChance += (experience / 100f) * 20f;
            
            if (isCompromised) baseChance *= 0.5f;
            
            return Mathf.Clamp(baseChance, 10f, 90f);
        }
        
        /// <summary>
        /// Calculate success chance for sabotage actions
        /// </summary>
        public float GetSabotageChance(SabotageType type)
        {
            float baseChance = 40f;
            baseChance += skill * 4f;
            baseChance += subtlety * 2f;
            
            // Network level improves chances
            baseChance += networkLevel * 10f;
            
            if (isCompromised) baseChance *= 0.6f;
            
            return Mathf.Clamp(baseChance, 15f, 85f);
        }
        
        /// <summary>
        /// Add experience and potentially improve skills
        /// </summary>
        public void AddExperience(int amount)
        {
            experience += amount;
            
            // Skill improvement thresholds
            if (experience > 100 && skill < 8) skill++;
            if (experience > 250 && subtlety < 9) subtlety++;
            if (experience > 400 && disguiseQuality < 9) disguiseQuality++;
        }
        
        /// <summary>
        /// Reset for new turn
        /// </summary>
        public void ResetTurn()
        {
            movementPoints = maxMovementPoints;
            
            if (turnsUntilSafe > 0)
            {
                turnsUntilSafe--;
                if (turnsUntilSafe <= 0)
                {
                    isCompromised = false;
                    suspicionLevel = Mathf.Max(0, suspicionLevel - 20);
                }
            }
            
            // Regain subtlety if hiding
            if (currentState == AgentState.Hiding)
            {
                suspicionLevel = Mathf.Max(0, suspicionLevel - 10);
            }
        }
    }

    /// <summary>
    /// Types of sabotage an agent can perform before battle
    /// </summary>
    public enum SabotageType
    {
        OpenGates,      // Reduces fortification effectiveness
        PoisonWells,    // Reduces enemy morale/stamina at battle start
        BurnSupplies,   // Reduces enemy ammo
        InciteDesertion,// Reduces enemy unit count slightly
        AssassinateGeneral, // Reduces enemy command bonuses
        SabotageArtillery, // Reduces enemy artillery effectiveness
        SpreadDisease, // Reduces enemy health at battle start
        BribeGarrison  // Some defenders don't fight
    }

    /// <summary>
    /// Intel report gathered by an agent
    /// </summary>
    [Serializable]
    public class IntelReport
    {
        public string reportId;
        public string provinceId;
        public string armyId;
        public IntelType intelType;
        public string content;
        public int turnGathered;
        public int freshness; // Decreases over time
        public bool isStale;
        
        // Detailed army info
        public int armySize;
        public int infantryCount;
        public int cavalryCount;
        public int artilleryCount;
        public float estimatedMorale;
        public float estimatedQuality;
        public string commanderName;
        public int commanderSkill;
        
        // City info
        public int garrisonSize;
        public int fortificationLevel;
        public float publicOrder;
        public bool hasRebelled;
        
        public IntelReport()
        {
            reportId = Guid.NewGuid().ToString().Substring(0, 8);
            turnGathered = CampaignManager.Instance?.CurrentTurn ?? 0;
            freshness = 10; // Good for 10 turns
        }
        
        public void UpdateFreshness(int currentTurn)
        {
            int age = currentTurn - turnGathered;
            freshness = Mathf.Max(0, 10 - age);
            isStale = freshness <= 0;
        }
    }

    public enum IntelType
    {
        ArmySize,
        ArmyComposition,
        ArmyMorale,
        GarrisonSize,
        FortificationStatus,
        CommanderInfo,
        PublicOrder,
        EconomicStatus,
        DiplomaticRelations,
        SecretPlans // Rare, high-value intel
    }

    /// <summary>
    /// Established spy network in a province
    /// </summary>
    [Serializable]
    public class SpyNetwork
    {
        public string provinceId;
        public FactionType ownerFaction;
        public FactionType controllingFaction;
        public int networkLevel; // 1-3
        public int turnsEstablished;
        public bool isActive;
        public int passiveIntelGain; // Intel gained each turn automatically
        
        // Bonuses for battles in this province
        public float siegeBonus; // Reduces fortification effectiveness
        public float moraleBonus; // Bonus to attacker morale
        public bool canSabotage;
        
        public SpyNetwork(string provinceId, FactionType owner, FactionType controller, int level)
        {
            this.provinceId = provinceId;
            this.ownerFaction = owner;
            this.controllingFaction = controller;
            this.networkLevel = level;
            this.turnsEstablished = 0;
            this.isActive = true;
            
            // Calculate bonuses based on level
            CalculateBonuses();
        }
        
        private void CalculateBonuses()
        {
            passiveIntelGain = networkLevel * 2;
            siegeBonus = networkLevel * 0.1f; // 10-30% reduction
            moraleBonus = networkLevel * 0.05f; // 5-15% bonus
            canSabotage = networkLevel >= 2;
        }
        
        public void Upgrade()
        {
            if (networkLevel < 3)
            {
                networkLevel++;
                CalculateBonuses();
            }
        }
    }

    /// <summary>
    /// Result of a sabotage action before battle
    /// </summary>
    public class SabotageResult
    {
        public bool success;
        public SabotageType type;
        public string description;
        public float battleModifier; // Applied to battle calculations
        public int enemyCasualtiesBeforeBattle;
        public float enemyMoraleReduction;
        public float enemyStaminaReduction;
        public bool gatesOpened;
        public int artilleryDisabled;
        
        public static SabotageResult Failed(string reason)
        {
            return new SabotageResult 
            { 
                success = false, 
                description = $"Sabotage failed: {reason}" 
            };
        }
    }
}
