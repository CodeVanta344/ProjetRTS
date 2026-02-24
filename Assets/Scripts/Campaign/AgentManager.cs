using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Manages all intelligence agents on the campaign map.
    /// Handles agent creation, movement, missions, and intelligence gathering.
    /// </summary>
    public class AgentManager : MonoBehaviour
    {
        public static AgentManager Instance { get; private set; }
        
        [Header("Agent Settings")]
        public int maxAgentsPerFaction = 5;
        public float baseDetectionChance = 0.05f;
        public int turnsToEstablishNetwork = 3;
        
        [Header("Costs")]
        public int spyRecruitCost = 500;
        public int diplomatRecruitCost = 400;
        public int assassinRecruitCost = 800;
        public int saboteurRecruitCost = 600;
        
        // Data storage
        private Dictionary<string, AgentData> agents = new Dictionary<string, AgentData>();
        private Dictionary<string, SpyNetwork> spyNetworks = new Dictionary<string, SpyNetwork>();
        private Dictionary<FactionType, List<IntelReport>> factionIntelligence = new Dictionary<FactionType, List<IntelReport>>();
        
        // Events
        public System.Action<AgentData, string> OnAgentMoved;
        public System.Action<AgentData, IntelReport> OnIntelGathered;
        public System.Action<AgentData, string> OnNetworkEstablished;
        public System.Action<AgentData, SabotageResult> OnSabotagePerformed;
        public System.Action<AgentData, string> OnAgentCaptured;
        public System.Action<AgentData> OnAgentKilled;
        
        private int agentIdCounter = 0;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        /// <summary>
        /// Create a new agent for a faction
        /// </summary>
        public AgentData CreateAgent(AgentType type, FactionType faction, string startingProvinceId)
        {
            // Check agent limit
            int currentAgents = agents.Values.Count(a => a.faction == faction);
            if (currentAgents >= maxAgentsPerFaction)
            {
                Debug.Log($"[AgentManager] Cannot create agent - {faction} already has {currentAgents} agents (max {maxAgentsPerFaction})");
                return null;
            }
            
            // Check cost
            int cost = GetRecruitmentCost(type);
            FactionData fd = CampaignManager.Instance?.Factions[faction];
            if (fd != null && !fd.CanAfford(cost))
            {
                Debug.Log($"[AgentManager] Cannot afford agent - costs {cost}g");
                return null;
            }
            
            // Pay cost
            fd?.Spend(cost);
            
            // Create agent
            agentIdCounter++;
            string agentId = $"{faction}_agent_{agentIdCounter}";
            string agentName = GenerateAgentName(type, faction);
            
            AgentData agent = new AgentData(agentId, agentName, type, faction);
            agent.currentProvinceId = startingProvinceId;
            
            // Get province position
            if (CampaignManager.Instance?.Provinces[startingProvinceId] is ProvinceData prov)
            {
                agent.mapPosition = prov.mapPosition;
            }
            
            agents[agentId] = agent;
            
            Debug.Log($"[AgentManager] Created {type} '{agentName}' for {faction} in {startingProvinceId}");
            
            return agent;
        }
        
        private int GetRecruitmentCost(AgentType type)
        {
            return type switch
            {
                AgentType.Spy => spyRecruitCost,
                AgentType.Diplomat => diplomatRecruitCost,
                AgentType.Assassin => assassinRecruitCost,
                AgentType.Saboteur => saboteurRecruitCost,
                _ => 500
            };
        }
        
        private string GenerateAgentName(AgentType type, FactionType faction)
        {
            string[] firstNames = { "Jean", "Pierre", "Marie", "Claude", "Antoine", "Louis", "Henri", "Francois", "Charles", "Joseph" };
            string[] lastNames = { "Dubois", "Martin", "Bernard", "Thomas", "Petit", "Robert", "Richard", "Durand", "Leroy", "Moreau" };
            
            string first = firstNames[Random.Range(0, firstNames.Length)];
            string last = lastNames[Random.Range(0, lastNames.Length)];
            string codeName = $"{first[0]}{last[0]}{Random.Range(10, 99)}";
            
            return type switch
            {
                AgentType.Spy => $"Agent {codeName}",
                AgentType.Diplomat => $"Envoy {first} {last}",
                AgentType.Assassin => $"Operative {codeName}",
                AgentType.Saboteur => $"Specialist {codeName}",
                _ => $"Agent {first}"
            };
        }
        
        /// <summary>
        /// Move an agent to a province
        /// </summary>
        public bool MoveAgent(AgentData agent, string targetProvinceId)
        {
            if (agent == null || string.IsNullOrEmpty(targetProvinceId)) return false;
            if (agent.currentState == AgentState.Captured || agent.currentState == AgentState.Dead) return false;
            
            // Get distance
            ProvinceData current = CampaignManager.Instance?.Provinces[agent.currentProvinceId];
            ProvinceData target = CampaignManager.Instance?.Provinces[targetProvinceId];
            
            if (current == null || target == null) return false;
            
            // Calculate movement cost (distance)
            float distance = Vector2.Distance(current.mapPosition, target.mapPosition);
            int moveCost = Mathf.Max(1, Mathf.RoundToInt(distance * 100)); // Scale to movement points
            
            if (!agent.CanMoveTo(moveCost))
            {
                Debug.Log($"[AgentManager] {agent.agentName} cannot reach {targetProvinceId} - needs {moveCost} MP, has {agent.movementPoints}");
                return false;
            }
            
            // Pay movement cost
            agent.movementPoints -= moveCost;
            
            // Update position
            string previousProvince = agent.currentProvinceId;
            agent.currentProvinceId = targetProvinceId;
            agent.mapPosition = target.mapPosition;
            agent.turnsInLocation = 0; // Reset for new location
            
            // Check if entering enemy territory
            bool isEnemyTerritory = target.owner != agent.faction;
            bool hasGarrison = target.garrisonSize > 0;
            
            if (isEnemyTerritory)
            {
                agent.currentState = AgentState.Infiltrating;
                
                // Roll for detection
                float detectionChance = agent.GetDetectionChance(true, hasGarrison);
                if (Random.value < detectionChance)
                {
                    HandleAgentDetected(agent, target);
                }
            }
            else
            {
                agent.currentState = AgentState.Idle;
            }
            
            OnAgentMoved?.Invoke(agent, targetProvinceId);
            Debug.Log($"[AgentManager] {agent.agentName} moved from {previousProvince} to {targetProvinceId}");
            
            return true;
        }
        
        /// <summary>
        /// Start gathering intelligence in current location
        /// </summary>
        public void StartIntelligenceGathering(AgentData agent)
        {
            if (agent == null) return;
            if (agent.currentState == AgentState.Captured || agent.currentState == AgentState.Dead) return;
            
            agent.currentState = AgentState.GatheringIntel;
            
            // Immediately gather some intel
            GatherIntel(agent);
            
            Debug.Log($"[AgentManager] {agent.agentName} started gathering intel in {agent.currentProvinceId}");
        }
        
        /// <summary>
        /// Gather intelligence from current location
        /// </summary>
        public IntelReport GatherIntel(AgentData agent)
        {
            if (agent == null) return null;
            
            ProvinceData province = CampaignManager.Instance?.Provinces[agent.currentProvinceId];
            if (province == null) return null;
            
            IntelReport report = new IntelReport();
            report.provinceId = agent.currentProvinceId;
            
            // Gather different types of intel based on agent skill and what's available
            List<IntelType> availableIntel = new List<IntelType>();
            
            // Always can get basic info
            availableIntel.Add(IntelType.GarrisonSize);
            availableIntel.Add(IntelType.FortificationStatus);
            
            // Army info if present
            var armiesInProvince = CampaignManager.Instance?.GetArmiesInProvince(agent.currentProvinceId);
            if (armiesInProvince != null && armiesInProvince.Count > 0)
            {
                availableIntel.Add(IntelType.ArmySize);
                if (agent.skill >= 4) availableIntel.Add(IntelType.ArmyComposition);
                if (agent.skill >= 6) availableIntel.Add(IntelType.ArmyMorale);
                if (agent.skill >= 7) availableIntel.Add(IntelType.CommanderInfo);
            }
            
            // Economic/diplomatic info
            if (agent.skill >= 5) availableIntel.Add(IntelType.EconomicStatus);
            if (agent.skill >= 6) availableIntel.Add(IntelType.PublicOrder);
            if (agent.skill >= 8) availableIntel.Add(IntelType.DiplomaticRelations);
            
            // Secret plans (rare, high skill)
            if (agent.skill >= 9 && Random.value < 0.2f) 
                availableIntel.Add(IntelType.SecretPlans);
            
            // Select random intel type from available
            IntelType selectedType = availableIntel[Random.Range(0, availableIntel.Count)];
            report.intelType = selectedType;
            
            // Fill report based on type
            FillIntelReport(report, selectedType, province, armiesInProvince, agent);
            
            // Add to agent's gathered intel
            agent.gatheredIntel.Add(report);
            
            // Add to faction intelligence
            if (!factionIntelligence.ContainsKey(agent.faction))
                factionIntelligence[agent.faction] = new List<IntelReport>();
            factionIntelligence[agent.faction].Add(report);
            
            // Award experience
            agent.AddExperience(10);
            
            OnIntelGathered?.Invoke(agent, report);
            Debug.Log($"[AgentManager] {agent.agentName} gathered {selectedType} intel in {province.provinceName}");
            
            return report;
        }
        
        private void FillIntelReport(IntelReport report, IntelType type, ProvinceData province, 
            List<ArmyData> armies, AgentData agent)
        {
            switch (type)
            {
                case IntelType.ArmySize:
                    if (armies != null && armies.Count > 0)
                    {
                        var army = armies[0]; // Report on largest army
                        report.armyId = army.armyId;
                        report.armySize = army.TotalSoldiers;
                        report.content = $"Army '{army.armyName}' has approximately {report.armySize} soldiers";
                    }
                    break;
                    
                case IntelType.ArmyComposition:
                    if (armies != null && armies.Count > 0)
                    {
                        var army = armies[0];
                        int infantry = army.regiments.Count(r => IsInfantry(r.unitType));
                        int cavalry = army.regiments.Count(r => IsCavalry(r.unitType));
                        int artillery = army.regiments.Count(r => r.unitType == UnitType.Artillery);
                        report.content = $"Composition: {infantry} infantry, {cavalry} cavalry, {artillery} artillery regiments";
                        report.infantryCount = infantry;
                        report.cavalryCount = cavalry;
                        report.artilleryCount = artillery;
                    }
                    break;
                    
                case IntelType.ArmyMorale:
                    report.estimatedMorale = Random.Range(0.4f, 0.9f);
                    string moraleDesc = report.estimatedMorale > 0.7f ? "high" : 
                                       report.estimatedMorale > 0.5f ? "moderate" : "low";
                    report.content = $"Enemy morale appears {moraleDesc} (~{(int)(report.estimatedMorale * 100)}%)";
                    break;
                    
                case IntelType.GarrisonSize:
                    report.garrisonSize = province.garrisonSize;
                    report.content = $"Garrison: {province.garrisonSize} troops defending {province.provinceName}";
                    break;
                    
                case IntelType.FortificationStatus:
                    // Use garrisonSize as proxy for fortification since ProvinceData doesn't have fortificationLevel
                    int fortLevel = province.garrisonSize > 50 ? 2 : province.garrisonSize > 20 ? 1 : 0;
                    report.fortificationLevel = fortLevel;
                    string fortDesc = fortLevel > 0 ? 
                        $"Level {fortLevel} fortifications present" : "No fortifications";
                    report.content = fortDesc;
                    break;
                    
                case IntelType.CommanderInfo:
                    if (armies != null && armies.Count > 0 && armies[0].regiments.Count > 0)
                    {
                        report.commanderName = $"General {GenerateGeneralName()}";
                        report.commanderSkill = Random.Range(1, 8);
                        report.content = $"Commanded by {report.commanderName} (Skill: {report.commanderSkill}/10)";
                    }
                    break;
                    
                case IntelType.PublicOrder:
                    report.publicOrder = Random.Range(0.3f, 0.9f);
                    string orderDesc = report.publicOrder > 0.7f ? "Content" : 
                                      report.publicOrder > 0.4f ? "Restless" : "Rebellious";
                    report.content = $"Public order: {orderDesc}";
                    break;
                    
                case IntelType.EconomicStatus:
                    report.content = $"Province generates moderate income. Trade activity observed.";
                    break;
                    
                case IntelType.DiplomaticRelations:
                    report.content = "Diplomatic rumors indicate shifting alliances...";
                    break;
                    
                case IntelType.SecretPlans:
                    string[] plans = {
                        "Enemy plans to attack within 3 turns!",
                        "Enemy is negotiating secret treaty with neutral power",
                        "Enemy army is understrength and vulnerable",
                        "Enemy commander is incompetent - good time to attack"
                    };
                    report.content = plans[Random.Range(0, plans.Length)];
                    break;
            }
        }
        
        private bool IsInfantry(UnitType type)
        {
            return type == UnitType.LineInfantry || type == UnitType.LightInfantry || type == UnitType.Grenadier;
        }
        
        private bool IsCavalry(UnitType type)
        {
            return type == UnitType.Cavalry || type == UnitType.Hussar || type == UnitType.Lancer;
        }
        
        private string GenerateGeneralName()
        {
            string[] names = { "Schwarz", "Blucher", "Wellington", "Soult", "Murat", "Ney", "Davout", "Kutuzov" };
            return names[Random.Range(0, names.Length)];
        }
        
        /// <summary>
        /// Start establishing a spy network in the current province
        /// </summary>
        public void StartNetworkEstablishment(AgentData agent)
        {
            if (agent == null) return;
            if (agent.hasEstablishedNetwork) return;
            
            ProvinceData province = CampaignManager.Instance?.Provinces[agent.currentProvinceId];
            if (province == null || province.owner == agent.faction) return; // Only in enemy territory
            
            agent.currentState = AgentState.EstablishingNetwork;
            agent.missionProgress = 0;
            agent.missionTargetTurns = turnsToEstablishNetwork;
            agent.networkProvinceId = agent.currentProvinceId;
            
            Debug.Log($"[AgentManager] {agent.agentName} started establishing network in {province.provinceName} ({turnsToEstablishNetwork} turns)");
        }
        
        /// <summary>
        /// Perform sabotage before a battle
        /// </summary>
        public SabotageResult PerformSabotage(AgentData agent, SabotageType type, string targetProvinceId)
        {
            if (agent == null) return SabotageResult.Failed("Invalid agent");
            if (!agent.hasEstablishedNetwork || agent.networkProvinceId != targetProvinceId)
                return SabotageResult.Failed("No established network in target province");
            
            // Check if agent is saboteur type or has high enough skill
            if (agent.agentType != AgentType.Saboteur && agent.skill < 7)
                return SabotageResult.Failed("Agent lacks sabotage expertise");
            
            // Roll for success
            float successChance = agent.GetSabotageChance(type);
            bool success = Random.value < (successChance / 100f);
            
            SabotageResult result = new SabotageResult
            {
                type = type,
                success = success
            };
            
            if (success)
            {
                agent.AddExperience(25);
                
                switch (type)
                {
                    case SabotageType.OpenGates:
                        result.description = "Agent opened the city gates! Fortifications bypassed.";
                        result.battleModifier = 0.25f;
                        result.gatesOpened = true;
                        break;
                        
                    case SabotageType.PoisonWells:
                        result.description = "Water supply poisoned! Enemy units start with reduced stamina.";
                        result.enemyStaminaReduction = 0.3f;
                        result.battleModifier = 0.15f;
                        break;
                        
                    case SabotageType.BurnSupplies:
                        result.description = "Supply depots burned! Enemy starts with 50% less ammo.";
                        result.battleModifier = 0.1f;
                        break;
                        
                    case SabotageType.InciteDesertion:
                        int deserters = Random.Range(5, 15);
                        result.description = $"{deserters} enemy soldiers deserted before the battle!";
                        result.enemyCasualtiesBeforeBattle = deserters;
                        result.battleModifier = 0.05f;
                        break;
                        
                    case SabotageType.AssassinateGeneral:
                        result.description = "Enemy general assassinated! Command penalties apply.";
                        result.battleModifier = 0.2f;
                        break;
                        
                    case SabotageType.SabotageArtillery:
                        result.artilleryDisabled = Random.Range(1, 3);
                        result.description = $"{result.artilleryDisabled} enemy artillery pieces sabotaged!";
                        result.battleModifier = 0.15f;
                        break;
                        
                    case SabotageType.SpreadDisease:
                        result.description = "Disease spread through enemy camp! Units start weakened.";
                        result.enemyStaminaReduction = 0.2f;
                        result.battleModifier = 0.1f;
                        break;
                        
                    case SabotageType.BribeGarrison:
                        result.description = "Part of the garrison was bribed! Fewer defenders.";
                        result.enemyCasualtiesBeforeBattle = 20;
                        result.battleModifier = 0.15f;
                        break;
                }
            }
            else
            {
                result.description = "Sabotage failed! The agent was compromised.";
                agent.isCompromised = true;
                agent.turnsUntilSafe = 3;
                agent.suspicionLevel += 30;
                
                // Risk of capture on failed sabotage
                if (Random.value < 0.3f)
                {
                    CaptureAgent(agent);
                }
            }
            
            OnSabotagePerformed?.Invoke(agent, result);
            Debug.Log($"[AgentManager] Sabotage {type} by {agent.agentName}: {(success ? "SUCCESS" : "FAILED")}");
            
            return result;
        }
        
        /// <summary>
        /// Handle end-of-turn processing for all agents
        /// </summary>
        public void ProcessEndOfTurn()
        {
            foreach (var agent in agents.Values)
            {
                ProcessAgentTurn(agent);
            }
            
            // Update intel freshness
            int currentTurn = CampaignManager.Instance?.CurrentTurn ?? 0;
            foreach (var reports in factionIntelligence.Values)
            {
                foreach (var report in reports)
                {
                    report.UpdateFreshness(currentTurn);
                }
            }
            
            // Clean up stale reports (older than 20 turns)
            foreach (var faction in factionIntelligence.Keys.ToList())
            {
                factionIntelligence[faction].RemoveAll(r => 
                    currentTurn - r.turnGathered > 20);
            }
        }
        
        private void ProcessAgentTurn(AgentData agent)
        {
            if (agent.currentState == AgentState.Captured || agent.currentState == AgentState.Dead)
                return;
            
            // Reset movement points
            agent.ResetTurn();
            
            // Increment turns in location
            agent.turnsInLocation++;
            
            // Process based on current state
            switch (agent.currentState)
            {
                case AgentState.GatheringIntel:
                    // Auto-gather intel every turn while in this state
                    if (Random.value < 0.7f) // 70% chance per turn
                    {
                        GatherIntel(agent);
                    }
                    
                    // Risk of detection increases with time
                    if (agent.turnsInLocation > 3)
                    {
                        ProvinceData prov = CampaignManager.Instance?.Provinces[agent.currentProvinceId];
                        if (prov != null && prov.owner != agent.faction)
                        {
                            float detectionRisk = agent.GetDetectionChance(true, prov.garrisonSize > 0);
                            detectionRisk += agent.turnsInLocation * 2f; // +2% per turn
                            
                            if (Random.value < (detectionRisk / 100f))
                            {
                                HandleAgentDetected(agent, prov);
                            }
                        }
                    }
                    break;
                    
                case AgentState.EstablishingNetwork:
                    agent.missionProgress++;
                    
                    // Check if complete
                    if (agent.missionProgress >= agent.missionTargetTurns)
                    {
                        // Roll for success
                        float chance = agent.GetNetworkEstablishChance();
                        if (Random.value < (chance / 100f))
                        {
                            // Success - establish network
                            agent.hasEstablishedNetwork = true;
                            agent.currentState = AgentState.GatheringIntel;
                            
                            ProvinceData prov = CampaignManager.Instance?.Provinces[agent.networkProvinceId];
                            
                            // Create network
                            SpyNetwork network = new SpyNetwork(
                                agent.networkProvinceId,
                                agent.faction,
                                prov?.owner ?? agent.faction, // Use agent's faction as fallback
                                1 // Level 1 to start
                            );
                            spyNetworks[agent.networkProvinceId] = network;
                            
                            // Agent gets better at being there
                            agent.AddExperience(50);
                            
                            OnNetworkEstablished?.Invoke(agent, agent.networkProvinceId);
                            Debug.Log($"[AgentManager] {agent.agentName} established Level 1 network in {prov?.provinceName}!");
                        }
                        else
                        {
                            // Failed - compromised but can retry
                            agent.isCompromised = true;
                            agent.turnsUntilSafe = 2;
                            agent.currentState = AgentState.Hiding;
                            Debug.Log($"[AgentManager] {agent.agentName} failed to establish network!");
                        }
                    }
                    break;
                    
                case AgentState.Hiding:
                    // Reduced detection risk
                    if (agent.turnsUntilSafe <= 0)
                    {
                        agent.currentState = AgentState.Idle;
                        Debug.Log($"[AgentManager] {agent.agentName} is no longer compromised");
                    }
                    break;
            }
        }
        
        private void HandleAgentDetected(AgentData agent, ProvinceData province)
        {
            agent.suspicionLevel += 20;
            
            // Roll for capture
            float captureChance = 30f + (agent.suspicionLevel * 0.5f);
            if (agent.subtlety > 5) captureChance -= 15f;
            if (agent.hasEstablishedNetwork) captureChance -= 10f;
            
            if (Random.value < (captureChance / 100f))
            {
                CaptureAgent(agent);
            }
            else
            {
                // Escaped but compromised
                agent.isCompromised = true;
                agent.turnsUntilSafe = 2;
                agent.currentState = AgentState.Hiding;
                Debug.Log($"[AgentManager] {agent.agentName} detected in {province.provinceName} but escaped!");
            }
        }
        
        private void CaptureAgent(AgentData agent)
        {
            agent.currentState = AgentState.Captured;
            OnAgentCaptured?.Invoke(agent, agent.currentProvinceId);
            Debug.Log($"[AgentManager] {agent.agentName} CAPTURED in {agent.currentProvinceId}!");
            
            // If had a network, it's now compromised
            if (agent.hasEstablishedNetwork && spyNetworks.ContainsKey(agent.networkProvinceId))
            {
                spyNetworks.Remove(agent.networkProvinceId);
                agent.hasEstablishedNetwork = false;
                Debug.Log($"[AgentManager] Spy network in {agent.networkProvinceId} destroyed!");
            }
        }
        
        /// <summary>
        /// Try to rescue a captured agent (diplomatic action or special mission)
        /// </summary>
        public bool AttemptRescue(AgentData agent)
        {
            if (agent.currentState != AgentState.Captured) return false;
            
            // 30% base chance + diplomat skill modifier
            float chance = 30f;
            
            if (Random.value < (chance / 100f))
            {
                agent.currentState = AgentState.Hiding;
                agent.isCompromised = true;
                agent.turnsUntilSafe = 5;
                Debug.Log($"[AgentManager] {agent.agentName} rescued!");
                return true;
            }
            
            // Failed rescue - agent might be executed
            if (Random.value < 0.3f)
            {
                KillAgent(agent);
            }
            
            return false;
        }
        
        /// <summary>
        /// Execute or kill an agent
        /// </summary>
        public void KillAgent(AgentData agent)
        {
            agent.currentState = AgentState.Dead;
            OnAgentKilled?.Invoke(agent);
            
            // Remove after a delay
            agents.Remove(agent.agentId);
            
            Debug.Log($"[AgentManager] {agent.agentName} has been KILLED");
        }
        
        // Getters for UI
        public List<AgentData> GetAgentsForFaction(FactionType faction)
        {
            return agents.Values.Where(a => a.faction == faction).ToList();
        }
        
        public List<AgentData> GetAllAgents()
        {
            return agents.Values.ToList();
        }
        
        public List<IntelReport> GetIntelligenceForFaction(FactionType faction)
        {
            if (!factionIntelligence.ContainsKey(faction))
                return new List<IntelReport>();
            return factionIntelligence[faction].Where(r => !r.isStale).ToList();
        }
        
        public SpyNetwork GetSpyNetwork(string provinceId)
        {
            return spyNetworks.ContainsKey(provinceId) ? spyNetworks[provinceId] : null;
        }
        
        public List<SpyNetwork> GetNetworksForFaction(FactionType faction)
        {
            return spyNetworks.Values.Where(n => n.ownerFaction == faction).ToList();
        }
        
        /// <summary>
        /// Get sabotage options available for a battle
        /// </summary>
        public List<SabotageType> GetAvailableSabotageOptions(string provinceId, FactionType faction)
        {
            List<SabotageType> options = new List<SabotageType>();
            
            // Check if we have a network there
            var network = GetSpyNetwork(provinceId);
            if (network == null || network.ownerFaction != faction) return options;
            
            // All networks allow basic sabotage
            options.Add(SabotageType.OpenGates);
            options.Add(SabotageType.BurnSupplies);
            
            if (network.networkLevel >= 2)
            {
                options.Add(SabotageType.PoisonWells);
                options.Add(SabotageType.InciteDesertion);
                options.Add(SabotageType.SabotageArtillery);
            }
            
            if (network.networkLevel >= 3)
            {
                options.Add(SabotageType.AssassinateGeneral);
                options.Add(SabotageType.SpreadDisease);
                options.Add(SabotageType.BribeGarrison);
            }
            
            return options;
        }
    }
}
