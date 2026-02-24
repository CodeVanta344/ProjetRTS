using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Agent/spy system for campaign. Manages agents and their covert operations.
    /// </summary>
    public class AgentSystem
    {
        private static AgentSystem _instance;
        public static AgentSystem Instance => _instance ??= new AgentSystem();

        private Dictionary<string, AgentData> agents = new Dictionary<string, AgentData>();
        private List<AgentMission> activeMissions = new List<AgentMission>();

        public Dictionary<string, AgentData> Agents => agents;

        public delegate void AgentEvent(AgentData agent, AgentMissionResult result);
        public event AgentEvent OnMissionComplete;

        #region Agent Management

        public AgentData RecruitAgent(FactionType faction, AgentType type, string locationId)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return null;

            // Check cost
            int cost = GetRecruitCost(type);
            if (!cm.Factions[faction].CanAfford(cost))
                return null;

            cm.Factions[faction].Spend(cost);

            string agentId = System.Guid.NewGuid().ToString();
            string agentName = GenerateAgentName(faction);
            
            var agent = new AgentData(agentId, agentName, type, faction);
            agent.currentProvinceId = locationId;

            agents[agent.agentId] = agent;
            Debug.Log($"[Agent] {faction} recruited {type}: {agent.agentName}");

            return agent;
        }

        public void RemoveAgent(string agentId)
        {
            if (agents.ContainsKey(agentId))
            {
                var agent = agents[agentId];
                agent.currentState = AgentState.Dead;
                agents.Remove(agentId);
                Debug.Log($"[Agent] {agent.agentName} removed");
            }
        }

        public List<AgentData> GetFactionAgents(FactionType faction)
        {
            var result = new List<AgentData>();
            foreach (var agent in agents.Values)
            {
                if (agent.faction == faction && agent.currentState != AgentState.Dead && agent.currentState != AgentState.Captured)
                    result.Add(agent);
            }
            return result;
        }

        private int GetRecruitCost(AgentType type)
        {
            switch (type)
            {
                case AgentType.Spy: return 500;
                case AgentType.Diplomat: return 400;
                case AgentType.Assassin: return 800;
                case AgentType.Saboteur: return 600;
                default: return 500;
            }
        }

        private string GenerateAgentName(FactionType faction)
        {
            string[] firstNames, lastNames;

            switch (faction)
            {
                case FactionType.France:
                    firstNames = new[] { "Jean", "Pierre", "Louis", "François", "Claude" };
                    lastNames = new[] { "Dubois", "Moreau", "Laurent", "Simon", "Michel" };
                    break;
                case FactionType.Britain:
                    firstNames = new[] { "John", "William", "Thomas", "James", "Robert" };
                    lastNames = new[] { "Smith", "Brown", "Wilson", "Taylor", "Davies" };
                    break;
                case FactionType.Prussia:
                    firstNames = new[] { "Friedrich", "Wilhelm", "Karl", "Heinrich", "Johann" };
                    lastNames = new[] { "Müller", "Schmidt", "Weber", "Wagner", "Becker" };
                    break;
                case FactionType.Russia:
                    firstNames = new[] { "Ivan", "Dmitri", "Alexei", "Nikolai", "Sergei" };
                    lastNames = new[] { "Petrov", "Ivanov", "Volkov", "Sokolov", "Kozlov" };
                    break;
                case FactionType.Austria:
                    firstNames = new[] { "Franz", "Johann", "Leopold", "Josef", "Anton" };
                    lastNames = new[] { "Gruber", "Huber", "Wagner", "Steiner", "Moser" };
                    break;
                case FactionType.Spain:
                    firstNames = new[] { "Carlos", "Miguel", "Antonio", "José", "Francisco" };
                    lastNames = new[] { "García", "Rodríguez", "Martínez", "López", "González" };
                    break;
                case FactionType.Ottoman:
                    firstNames = new[] { "Mehmet", "Ahmet", "Mustafa", "Ali", "Hasan" };
                    lastNames = new[] { "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin" };
                    break;
                default:
                    firstNames = new[] { "Agent" };
                    lastNames = new[] { "Unknown" };
                    break;
            }

            return $"{firstNames[Random.Range(0, firstNames.Length)]} {lastNames[Random.Range(0, lastNames.Length)]}";
        }

        #endregion

        #region Movement

        public bool MoveAgent(string agentId, string targetLocationId)
        {
            if (!agents.TryGetValue(agentId, out var agent))
                return false;

            if (agent.currentState == AgentState.Dead || agent.currentState == AgentState.Captured)
                return false;

            agent.currentProvinceId = targetLocationId;
            agent.movementPoints--;

            Debug.Log($"[Agent] {agent.agentName} moved to {targetLocationId}");
            return true;
        }

        #endregion

        #region Missions

        public bool StartMission(string agentId, AgentMissionType missionType, string targetId)
        {
            if (!agents.TryGetValue(agentId, out var agent))
                return false;

            if (agent.currentState == AgentState.Dead || agent.currentState == AgentState.Captured)
                return false;

            // Validate mission type for agent type
            if (!CanPerformMission(agent.agentType, missionType))
                return false;

            var mission = new AgentMission
            {
                missionId = System.Guid.NewGuid().ToString(),
                agentId = agentId,
                missionType = missionType,
                targetId = targetId,
                turnsRemaining = GetMissionDuration(missionType),
                startTurn = CampaignManager.Instance?.CurrentTurn ?? 0
            };

            activeMissions.Add(mission);
            agent.targetProvinceId = targetId;

            Debug.Log($"[Agent] {agent.agentName} started mission: {missionType}");
            return true;
        }

        private bool CanPerformMission(AgentType agentType, AgentMissionType missionType)
        {
            switch (agentType)
            {
                case AgentType.Spy:
                    return missionType == AgentMissionType.Reconnaissance ||
                           missionType == AgentMissionType.InfiltrateArmy ||
                           missionType == AgentMissionType.InfiltrateCity ||
                           missionType == AgentMissionType.CounterEspionage;

                case AgentType.Diplomat:
                    return missionType == AgentMissionType.ImproveRelations ||
                           missionType == AgentMissionType.BribeOfficial ||
                           missionType == AgentMissionType.NegotiateTreaty;

                case AgentType.Assassin:
                    return missionType == AgentMissionType.Assassinate ||
                           missionType == AgentMissionType.Sabotage;

                case AgentType.Saboteur:
                    return missionType == AgentMissionType.Sabotage ||
                           missionType == AgentMissionType.InciteRebellion ||
                           missionType == AgentMissionType.DestroyBuilding;

                default:
                    return false;
            }
        }

        private int GetMissionDuration(AgentMissionType missionType)
        {
            switch (missionType)
            {
                case AgentMissionType.Reconnaissance: return 1;
                case AgentMissionType.InfiltrateArmy: return 2;
                case AgentMissionType.InfiltrateCity: return 2;
                case AgentMissionType.Assassinate: return 1;
                case AgentMissionType.Sabotage: return 1;
                case AgentMissionType.InciteRebellion: return 3;
                case AgentMissionType.ImproveRelations: return 2;
                case AgentMissionType.BribeOfficial: return 1;
                case AgentMissionType.CounterEspionage: return 1;
                case AgentMissionType.DestroyBuilding: return 2;
                case AgentMissionType.SpreadReligion: return 3;
                case AgentMissionType.ImprovePublicOrder: return 2;
                case AgentMissionType.NegotiateTreaty: return 2;
                default: return 1;
            }
        }

        #endregion

        #region Turn Processing

        public void ProcessTurn()
        {
            var completedMissions = new List<AgentMission>();

            foreach (var mission in activeMissions)
            {
                mission.turnsRemaining--;

                if (mission.turnsRemaining <= 0)
                {
                    completedMissions.Add(mission);
                }
            }

            foreach (var mission in completedMissions)
            {
                ResolveMission(mission);
                activeMissions.Remove(mission);
            }

            // Reset agent movement
            foreach (var agent in agents.Values)
            {
                if (agent.currentState != AgentState.Dead)
                    agent.movementPoints = agent.maxMovementPoints;
            }
        }

        private void ResolveMission(AgentMission mission)
        {
            if (!agents.TryGetValue(mission.agentId, out var agent))
                return;

            // Clear mission state
            agent.currentState = AgentState.Idle;

            // Calculate success chance
            float successChance = CalculateSuccessChance(agent, mission);
            bool success = Random.value < successChance;

            // Calculate detection chance
            float detectionChance = CalculateDetectionChance(agent, mission);
            bool detected = Random.value < detectionChance;

            var result = new AgentMissionResult
            {
                success = success,
                detected = detected,
                agentCaptured = detected && Random.value < 0.5f,
                agentKilled = detected && Random.value < 0.2f
            };

            // Apply mission effects
            if (success)
            {
                ApplyMissionSuccess(agent, mission);
                agent.AddExperience(GetMissionExpReward(mission.missionType));
            }

            // Handle agent fate
            if (result.agentKilled)
            {
                RemoveAgent(agent.agentId);
                Debug.Log($"[Agent] {agent.agentName} was killed during mission!");
            }
            if (result.agentCaptured)
            {
                agent.currentState = AgentState.Captured;
                Debug.Log($"[Agent] {agent.agentName} was captured!");
            }
            else if (detected)
            {
                // Detected but escaped - diplomatic incident
                var targetFaction = GetTargetFaction(mission.targetId);
                if (targetFaction.HasValue)
                {
                    DiplomacySystem.Instance?.ModifyRelationScore(agent.faction, targetFaction.Value, -10);
                }
                Debug.Log($"[Agent] {agent.agentName} was detected but escaped!");
            }

            OnMissionComplete?.Invoke(agent, result);

            string resultText = success ? "SUCCESS" : "FAILED";
            Debug.Log($"[Agent] Mission {mission.missionType} by {agent.agentName}: {resultText}");
        }

        private float CalculateSuccessChance(AgentData agent, AgentMission mission)
        {
            float baseChance = 0.5f;

            // Agent skill bonus
            baseChance += agent.skill * 0.05f;

            // Experience bonus (using skill as proxy since GetLevel doesn't exist)
            baseChance += (agent.experience / 100f) * 0.03f;

            // Mission difficulty modifier
            baseChance -= GetMissionDifficulty(mission.missionType) * 0.1f;

            return Mathf.Clamp(baseChance, 0.1f, 0.95f);
        }

        private float CalculateDetectionChance(AgentData agent, AgentMission mission)
        {
            float baseChance = 0.3f;

            // Agent skill reduces detection
            baseChance -= agent.skill * 0.03f;

            // Mission type affects detection
            switch (mission.missionType)
            {
                case AgentMissionType.Assassinate:
                case AgentMissionType.Sabotage:
                case AgentMissionType.InciteRebellion:
                    baseChance += 0.2f;
                    break;
                case AgentMissionType.Reconnaissance:
                case AgentMissionType.ImproveRelations:
                    baseChance -= 0.1f;
                    break;
            }

            // Counter-espionage in target location
            if (HasCounterEspionage(mission.targetId))
                baseChance += 0.2f;

            return Mathf.Clamp(baseChance, 0.05f, 0.8f);
        }

        private int GetMissionDifficulty(AgentMissionType missionType)
        {
            switch (missionType)
            {
                case AgentMissionType.Reconnaissance: return 1;
                case AgentMissionType.ImproveRelations: return 1;
                case AgentMissionType.ImprovePublicOrder: return 1;
                case AgentMissionType.InfiltrateArmy: return 2;
                case AgentMissionType.InfiltrateCity: return 2;
                case AgentMissionType.BribeOfficial: return 2;
                case AgentMissionType.Sabotage: return 3;
                case AgentMissionType.InciteRebellion: return 4;
                case AgentMissionType.Assassinate: return 5;
                case AgentMissionType.DestroyBuilding: return 4;
                default: return 2;
            }
        }

        private int GetMissionExpReward(AgentMissionType missionType)
        {
            return GetMissionDifficulty(missionType) * 20;
        }

        private bool HasCounterEspionage(string locationId)
        {
            foreach (var agent in agents.Values)
            {
                if (agent.currentState == AgentState.Dead || agent.currentState == AgentState.Captured) continue;
                if (agent.currentProvinceId != locationId) continue;
                if (agent.targetProvinceId == locationId)
                {
                    var mission = activeMissions.Find(m => m.targetId == locationId);
                    if (mission != null && mission.missionType == AgentMissionType.CounterEspionage)
                        return true;
                }
            }
            return false;
        }

        private FactionType? GetTargetFaction(string targetId)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return null;

            // Check if it's a province
            if (cm.Provinces.TryGetValue(targetId, out var province))
                return province.owner;

            // Check if it's a city
            if (cm.Cities.TryGetValue(targetId, out var city))
                return city.owner;

            // Check if it's an army
            if (cm.Armies.TryGetValue(targetId, out var army))
                return army.faction;

            return null;
        }

        #endregion

        #region Mission Effects

        private void ApplyMissionSuccess(AgentData agent, AgentMission mission)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            switch (mission.missionType)
            {
                case AgentMissionType.Reconnaissance:
                    // Reveal army composition or city info
                    Debug.Log($"[Agent] Reconnaissance complete on {mission.targetId}");
                    break;

                case AgentMissionType.InfiltrateArmy:
                    // Reveal army location and composition for several turns
                    if (cm.Armies.TryGetValue(mission.targetId, out var army))
                    {
                        Debug.Log($"[Agent] Infiltrated {army.armyName}: {army.TotalSoldiers} soldiers");
                    }
                    break;

                case AgentMissionType.InfiltrateCity:
                    // Reveal city buildings and garrison
                    if (cm.Cities.TryGetValue(mission.targetId, out var city))
                    {
                        Debug.Log($"[Agent] Infiltrated {city.cityName}: Pop {city.population}, Garrison {city.garrisonSize}");
                    }
                    break;

                case AgentMissionType.Assassinate:
                    // Kill target general or agent
                    // Would need to implement general targeting
                    Debug.Log($"[Agent] Assassination successful!");
                    break;

                case AgentMissionType.Sabotage:
                    // Damage building or reduce production
                    if (cm.Cities.TryGetValue(mission.targetId, out var sabotageCity))
                    {
                        sabotageCity.publicOrder -= 20f;
                        Debug.Log($"[Agent] Sabotaged {sabotageCity.cityName}!");
                    }
                    break;

                case AgentMissionType.InciteRebellion:
                    // Reduce public order significantly, possibly spawn rebels
                    if (cm.Cities.TryGetValue(mission.targetId, out var rebellionCity))
                    {
                        rebellionCity.publicOrder -= 40f;
                        if (rebellionCity.publicOrder < 20f)
                        {
                            // Rebellion!
                            Debug.Log($"[Agent] Rebellion incited in {rebellionCity.cityName}!");
                        }
                    }
                    break;

                case AgentMissionType.ImproveRelations:
                    // Improve diplomatic relations
                    var targetFaction = GetTargetFaction(mission.targetId);
                    if (targetFaction.HasValue)
                    {
                        DiplomacySystem.Instance?.ModifyRelationScore(agent.faction, targetFaction.Value, 15);
                    }
                    break;

                case AgentMissionType.BribeOfficial:
                    // Gain gold or information
                    cm.Factions[agent.faction].gold += 200f;
                    Debug.Log($"[Agent] Bribe successful! Gained 200 gold.");
                    break;

                case AgentMissionType.DestroyBuilding:
                    // Destroy a building in target city
                    if (cm.Cities.TryGetValue(mission.targetId, out var destroyCity))
                    {
                        if (destroyCity.buildings.Count > 0)
                        {
                            destroyCity.buildings.RemoveAt(Random.Range(0, destroyCity.buildings.Count));
                            Debug.Log($"[Agent] Building destroyed in {destroyCity.cityName}!");
                        }
                    }
                    break;

                case AgentMissionType.ImprovePublicOrder:
                    // Increase public order
                    if (cm.Cities.TryGetValue(mission.targetId, out var orderCity))
                    {
                        orderCity.publicOrder += 15f;
                        Debug.Log($"[Agent] Public order improved in {orderCity.cityName}");
                    }
                    break;

                case AgentMissionType.CounterEspionage:
                    // Already handled by detection chance modifier
                    Debug.Log($"[Agent] Counter-espionage active in {mission.targetId}");
                    break;
            }
        }

        #endregion
        
        #region Mission Data Classes
        
        public enum AgentMissionType
        {
            Reconnaissance,
            InfiltrateArmy,
            InfiltrateCity,
            Assassinate,
            Sabotage,
            InciteRebellion,
            ImproveRelations,
            BribeOfficial,
            CounterEspionage,
            DestroyBuilding,
            SpreadReligion,
            ImprovePublicOrder,
            NegotiateTreaty
        }
        
        public class AgentMission
        {
            public string missionId;
            public string agentId;
            public AgentMissionType missionType;
            public string targetId;
            public int turnsRemaining;
            public int startTurn;
        }
        
        public class AgentMissionResult
        {
            public bool success;
            public bool detected;
            public bool agentCaptured;
            public bool agentKilled;
        }
        
        #endregion
    }
}
