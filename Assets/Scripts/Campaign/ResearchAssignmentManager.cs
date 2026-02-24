using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Manages the assignable research system.
    /// Handles recruitment of researchers, creation of research projects,
    /// assignment of researchers to projects, and turn-by-turn progress.
    /// </summary>
    public class ResearchAssignmentManager : MonoBehaviour
    {
        public static ResearchAssignmentManager Instance { get; private set; }
        
        [Header("Research Settings")]
        public int maxResearchersPerFaction = 10;
        public int baseResearchPointsForSimpleTech = 100;
        public int baseResearchPointsForComplexTech = 200;
        
        [Header("Building Slots")]
        public int academySlots = 2;
        public int universitySlots = 4;
        public int royalAcademySlots = 6;
        
        // Data storage
        private Dictionary<string, Researcher> researchers = new Dictionary<string, Researcher>();
        private Dictionary<string, ResearchProject> activeProjects = new Dictionary<string, ResearchProject>();
        private Dictionary<string, ResearchBuilding> researchBuildings = new Dictionary<string, ResearchBuilding>();
        
        // Faction-specific tracking
        private Dictionary<FactionType, List<string>> factionResearchers = new Dictionary<FactionType, List<string>>();
        private Dictionary<FactionType, List<string>> factionProjects = new Dictionary<FactionType, List<string>>();
        
        // Events
        public System.Action<Researcher> OnResearcherRecruited;
        public System.Action<ResearchProject> OnProjectStarted;
        public System.Action<ResearchProject> OnProjectCompleted;
        public System.Action<Researcher, ResearchProject> OnResearcherAssigned;
        public System.Action<Researcher, ResearchProject> OnResearcherUnassigned;
        public System.Action<Technology, FactionType> OnResearchCompleted;
        
        private int researcherIdCounter = 0;
        
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
        /// Recruit a new researcher for a faction
        /// </summary>
        public Researcher RecruitResearcher(ResearcherType type, FactionType faction, string provinceId, bool autoPay = true)
        {
            // Check limit
            int currentCount = GetResearcherCountForFaction(faction);
            if (currentCount >= maxResearchersPerFaction)
            {
                Debug.Log($"[ResearchManager] Cannot recruit - {faction} already has {currentCount} researchers (max {maxResearchersPerFaction})");
                return null;
            }
            
            // Check if province has research building
            if (!HasResearchBuilding(provinceId))
            {
                Debug.Log($"[ResearchManager] Cannot recruit - no research building in {provinceId}");
                return null;
            }
            
            // Check cost
            int cost = Researcher.GetRecruitmentCost(type);
            if (autoPay)
            {
                FactionData fd = CampaignManager.Instance?.Factions[faction];
                if (fd == null || !fd.CanAfford(cost))
                {
                    Debug.Log($"[ResearchManager] Cannot afford researcher - costs {cost}g");
                    return null;
                }
                fd.Spend(cost);
            }
            
            // Create researcher
            researcherIdCounter++;
            string id = $"{faction}_researcher_{researcherIdCounter}";
            string name = GenerateResearcherName(type, faction);
            
            Researcher researcher = new Researcher(id, name, type, faction);
            researcher.currentProvinceId = provinceId;
            
            researchers[id] = researcher;
            
            if (!factionResearchers.ContainsKey(faction))
                factionResearchers[faction] = new List<string>();
            factionResearchers[faction].Add(id);
            
            // Assign to building
            AssignResearcherToBuilding(researcher, provinceId);
            
            OnResearcherRecruited?.Invoke(researcher);
            Debug.Log($"[ResearchManager] Recruited {Researcher.GetTypeName(type)} '{name}' for {faction} in {provinceId}");
            
            return researcher;
        }
        
        private string GenerateResearcherName(ResearcherType type, FactionType faction)
        {
            string[] firstNames = { 
                "Jean", "Pierre", "Antoine", "Louis", "Henri", "Charles", "François", "Joseph",
                "Marie", "Anne", "Sophie", "Élise", "Catherine", "Marguerite"
            };
            string[] lastNames = { 
                "Bourbon", "Orléans", "Montesquieu", "Voltaire", "Rousseau", "Lavoisier",
                "Laplace", "Cuvier", "Berthollet", "Monge", "Condorcet", "Diderot"
            };
            
            string first = firstNames[Random.Range(0, firstNames.Length)];
            string last = lastNames[Random.Range(0, lastNames.Length)];
            
            return type switch
            {
                ResearcherType.Gentleman => $"Lord {last}",
                ResearcherType.Scholar => $"Prof. {first} {last}",
                ResearcherType.Scientist => $"Dr. {first} {last}",
                ResearcherType.MilitaryExpert => $"Col. {last}",
                ResearcherType.Economist => $"{first} {last}",
                ResearcherType.DiplomatExpert => $"Amb. {last}",
                _ => $"{first} {last}"
            };
        }
        
        /// <summary>
        /// Start a new research project
        /// </summary>
        public ResearchProject StartResearchProject(string techId, string techName, TechCategory category, 
            float researchPointsRequired, int goldCost, FactionType faction, string provinceId)
        {
            // Check if already researching this tech
            if (IsResearchingTech(techId, faction))
            {
                Debug.Log($"[ResearchManager] Already researching {techName}");
                return null;
            }
            
            // Check province ownership
            ProvinceData prov = CampaignManager.Instance?.Provinces[provinceId];
            if (prov == null || prov.owner != faction)
            {
                Debug.Log($"[ResearchManager] Cannot start research - don't control {provinceId}");
                return null;
            }
            
            // Check for research building
            if (!HasResearchBuilding(provinceId))
            {
                Debug.Log($"[ResearchManager] No research building in {provinceId}");
                return null;
            }
            
            // Pay initial cost
            FactionData fd = CampaignManager.Instance?.Factions[faction];
            if (fd == null || !fd.CanAfford(goldCost))
            {
                Debug.Log($"[ResearchManager] Cannot afford research - costs {goldCost}g");
                return null;
            }
            fd.Spend(goldCost);
            
            // Create project
            ResearchProject project = new ResearchProject(techId, techName, category, 
                researchPointsRequired, goldCost, provinceId);
            project.goldPaid = goldCost;
            
            activeProjects[project.projectId] = project;
            
            if (!factionProjects.ContainsKey(faction))
                factionProjects[faction] = new List<string>();
            factionProjects[faction].Add(project.projectId);
            
            OnProjectStarted?.Invoke(project);
            Debug.Log($"[ResearchManager] Started research project: {techName} in {prov.provinceName} ({researchPointsRequired} pts required)");
            
            return project;
        }
        
        /// <summary>
        /// Start research from Technology data
        /// </summary>
        public ResearchProject StartResearchProject(Technology tech, FactionType faction, string provinceId)
        {
            // Convert turns to research points
            // Simple formula: turns * 50 points per turn (base)
            float pointsRequired = tech.turnsToResearch * 50f;
            
            // Adjust cost based on technology
            int goldCost = tech.researchCost;
            
            return StartResearchProject(tech.id, tech.name, tech.category, pointsRequired, goldCost, faction, provinceId);
        }
        
        /// <summary>
        /// Assign a researcher to a project
        /// </summary>
        public bool AssignResearcherToProject(string researcherId, string projectId)
        {
            if (!researchers.ContainsKey(researcherId) || !activeProjects.ContainsKey(projectId))
                return false;
            
            Researcher researcher = researchers[researcherId];
            ResearchProject project = activeProjects[projectId];
            
            // Check faction match
            if (researcher.faction != GetProjectFaction(project))
            {
                Debug.Log($"[ResearchManager] Cannot assign - researcher and project have different factions");
                return false;
            }
            
            // Unassign from current project if any
            if (researcher.isAssigned)
            {
                UnassignResearcher(researcherId);
            }
            
            // Check if project already has max researchers (soft limit for balance)
            if (project.GetResearcherCount() >= 6)
            {
                Debug.Log($"[ResearchManager] Project already has {project.GetResearcherCount()} researchers (diminishing returns too high)");
                // Still allow but warn
            }
            
            // Assign
            project.AssignResearcher(researcher);
            
            OnResearcherAssigned?.Invoke(researcher, project);
            Debug.Log($"[ResearchManager] Assigned {researcher.name} to {project.techName}");
            
            return true;
        }
        
        /// <summary>
        /// Unassign a researcher from their current project
        /// </summary>
        public void UnassignResearcher(string researcherId)
        {
            if (!researchers.ContainsKey(researcherId)) return;
            
            Researcher researcher = researchers[researcherId];
            if (!researcher.isAssigned) return;
            
            string projectId = researcher.assignedProjectId;
            if (activeProjects.ContainsKey(projectId))
            {
                ResearchProject project = activeProjects[projectId];
                project.UnassignResearcher(researcher);
                OnResearcherUnassigned?.Invoke(researcher, project);
            }
            else
            {
                researcher.Unassign();
            }
        }
        
        /// <summary>
        /// Process end of turn - advance all research projects
        /// </summary>
        public void ProcessEndOfTurn()
        {
            List<ResearchProject> completedProjects = new List<ResearchProject>();
            
            foreach (var project in activeProjects.Values.ToList())
            {
                if (project.isComplete) continue;
                
                // Get assigned researchers
                List<Researcher> assignedResearchers = new List<Researcher>();
                foreach (string rid in project.assignedResearcherIds)
                {
                    if (researchers.ContainsKey(rid))
                        assignedResearchers.Add(researchers[rid]);
                }
                
                // Calculate progress
                float points = project.CalculateProgress(assignedResearchers);
                
                // Apply building efficiency bonus
                if (researchBuildings.TryGetValue(project.buildingId, out var building))
                {
                    float buildingBonus = building.GetEfficiencyBonusForCategory(project.category);
                    points *= (1f + buildingBonus);
                }
                
                // Add progress
                bool justCompleted = project.AddProgress(points);
                
                // Pay upkeep
                int upkeep = project.GetUpkeepCostPerTurn();
                FactionType faction = GetProjectFaction(project);
                FactionData fd = CampaignManager.Instance?.Factions[faction];
                if (fd != null && fd.gold >= upkeep)
                {
                    fd.Spend(upkeep);
                }
                else
                {
                    // Cannot afford upkeep - research stalls
                    Debug.Log($"[ResearchManager] {project.techName} stalled - cannot pay upkeep");
                }
                
                // Award experience to researchers
                foreach (var r in assignedResearchers)
                {
                    r.AddExperience(5); // 5 XP per turn
                }
                
                if (justCompleted)
                {
                    completedProjects.Add(project);
                    OnProjectCompleted?.Invoke(project);
                }
            }
            
            // Notify TechTree of completions
            foreach (var project in completedProjects)
            {
                FactionType faction = GetProjectFaction(project);
                TechTree techTree = CampaignManager.Instance?.Factions[faction]?.techTree;
                if (techTree != null)
                {
                    // Complete the tech in the tech tree
                    techTree.ForceCompleteTech(project.techId);
                    
                    // Get technology for notification
                    Technology tech = techTree.GetAllTechs()[project.techId];
                    OnResearchCompleted?.Invoke(tech, faction);
                    
                    Debug.Log($"[ResearchManager] Research completed: {project.techName}!");
                }
                
                // Clean up completed project
                activeProjects.Remove(project.projectId);
                if (factionProjects.ContainsKey(faction))
                    factionProjects[faction].Remove(project.projectId);
            }
        }
        
        /// <summary>
        /// Create a research building in a province
        /// </summary>
        public ResearchBuilding CreateResearchBuilding(string provinceId, string buildingType)
        {
            ProvinceData prov = CampaignManager.Instance?.Provinces[provinceId];
            if (prov == null) return null;
            
            string buildingId = $"{provinceId}_{buildingType}";
            
            ResearchBuilding building = buildingType.ToLower() switch
            {
                "academy" => new ResearchBuilding(buildingId, provinceId, "Academy", academySlots, 0f),
                "university" => new ResearchBuilding(buildingId, provinceId, "University", universitySlots, 0.1f),
                "royal_academy" => new ResearchBuilding(buildingId, provinceId, "Royal Academy", royalAcademySlots, 0.2f),
                "military_academy" => new ResearchBuilding(buildingId, provinceId, "Military Academy", academySlots, 0.15f)
                { categoryFocus = TechCategory.Military, categoryBonus = 0.2f },
                "economic_college" => new ResearchBuilding(buildingId, provinceId, "Economic College", academySlots, 0.15f)
                { categoryFocus = TechCategory.Economy, categoryBonus = 0.2f },
                _ => new ResearchBuilding(buildingId, provinceId, "Research Building", academySlots, 0f)
            };
            
            researchBuildings[buildingId] = building;
            Debug.Log($"[ResearchManager] Created {building.buildingName} in {prov.provinceName} ({building.maxResearchers} slots)");
            
            return building;
        }
        
        private void AssignResearcherToBuilding(Researcher researcher, string provinceId)
        {
            // Find a building in the province with available slots
            foreach (var building in researchBuildings.Values)
            {
                if (building.provinceId == provinceId && building.HasAvailableSlot())
                {
                    building.assignedResearcherIds.Add(researcher.researcherId);
                    researcher.currentProvinceId = provinceId;
                    return;
                }
            }
        }
        
        /// <summary>
        /// Check if a province has a research building
        /// </summary>
        public bool HasResearchBuilding(string provinceId)
        {
            return researchBuildings.Values.Any(b => b.provinceId == provinceId);
        }
        
        /// <summary>
        /// Get research buildings in a province
        /// </summary>
        public List<ResearchBuilding> GetResearchBuildingsInProvince(string provinceId)
        {
            return researchBuildings.Values.Where(b => b.provinceId == provinceId).ToList();
        }
        
        /// <summary>
        /// Get all researchers for a faction
        /// </summary>
        public List<Researcher> GetResearchersForFaction(FactionType faction)
        {
            if (!factionResearchers.ContainsKey(faction))
                return new List<Researcher>();
            
            return factionResearchers[faction]
                .Where(id => researchers.ContainsKey(id))
                .Select(id => researchers[id])
                .ToList();
        }
        
        /// <summary>
        /// Get idle (unassigned) researchers for a faction
        /// </summary>
        public List<Researcher> GetIdleResearchers(FactionType faction)
        {
            return GetResearchersForFaction(faction).Where(r => r.isIdle).ToList();
        }
        
        /// <summary>
        /// Get active research projects for a faction
        /// </summary>
        public List<ResearchProject> GetProjectsForFaction(FactionType faction)
        {
            if (!factionProjects.ContainsKey(faction))
                return new List<ResearchProject>();
            
            return factionProjects[faction]
                .Where(id => activeProjects.ContainsKey(id))
                .Select(id => activeProjects[id])
                .Where(p => !p.isComplete)
                .ToList();
        }
        
        /// <summary>
        /// Check if a tech is currently being researched
        /// </summary>
        public bool IsResearchingTech(string techId, FactionType faction)
        {
            var projects = GetProjectsForFaction(faction);
            return projects.Any(p => p.techId == techId);
        }
        
        /// <summary>
        /// Get researcher count for a faction
        /// </summary>
        public int GetResearcherCountForFaction(FactionType faction)
        {
            if (!factionResearchers.ContainsKey(faction)) return 0;
            return factionResearchers[faction].Count;
        }
        
        /// <summary>
        /// Get faction for a project
        /// </summary>
        private FactionType GetProjectFaction(ResearchProject project)
        {
            // Find first researcher and get their faction
            if (project.assignedResearcherIds.Count > 0)
            {
                string firstResearcherId = project.assignedResearcherIds[0];
                if (researchers.ContainsKey(firstResearcherId))
                    return researchers[firstResearcherId].faction;
            }
            
            // Fallback - check province owner
            ProvinceData prov = CampaignManager.Instance?.Provinces[project.provinceId];
            if (prov != null)
                return prov.owner;
            
            // Default fallback - should not reach here normally
            return FactionType.France;
        }
        
        /// <summary>
        /// Calculate total research output for a faction
        /// </summary>
        public float GetTotalResearchOutput(FactionType faction)
        {
            float total = 0f;
            var researchers = GetResearchersForFaction(faction);
            
            foreach (var r in researchers)
            {
                if (r.isAssigned)
                {
                    // Get project category for efficiency calculation
                    if (activeProjects.TryGetValue(r.assignedProjectId, out var project))
                    {
                        total += r.GetResearchPointsPerTurn(project.category);
                    }
                }
            }
            
            return total;
        }
        
        /// <summary>
        /// Dismiss (fire) a researcher
        /// </summary>
        public void DismissResearcher(string researcherId)
        {
            if (!researchers.ContainsKey(researcherId)) return;
            
            Researcher r = researchers[researcherId];
            
            // Unassign from project
            if (r.isAssigned)
            {
                UnassignResearcher(researcherId);
            }
            
            // Remove from building
            foreach (var building in researchBuildings.Values)
            {
                building.assignedResearcherIds.Remove(researcherId);
            }
            
            // Remove from faction list
            if (factionResearchers.ContainsKey(r.faction))
                factionResearchers[r.faction].Remove(researcherId);
            
            // Remove from main dictionary
            researchers.Remove(researcherId);
            
            Debug.Log($"[ResearchManager] Dismissed researcher {r.name}");
        }
    }
}
