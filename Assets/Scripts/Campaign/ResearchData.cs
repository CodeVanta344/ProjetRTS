using System;
using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Types of research personnel (gentlemen, scholars, scientists)
    /// </summary>
    public enum ResearcherType
    {
        Gentleman,      // Noble amateur researcher, slower but cheaper
        Scholar,        // Professional academic, standard speed
        Scientist,      // Expert researcher, faster but expensive to recruit
        MilitaryExpert, // Specializes in military tech
        Economist,      // Specializes in economic tech
        DiplomatExpert  // Specializes in diplomatic tech
    }

    /// <summary>
    /// Represents a researcher (gentleman/scholar) who can be assigned to research projects.
    /// Each researcher provides research points per turn.
    /// </summary>
    [Serializable]
    public class Researcher
    {
        public string researcherId;
        public string name;
        public ResearcherType type;
        public FactionType faction;
        
        // Stats
        public int skill;           // 1-10, affects research speed multiplier
        public int experience;      // Gained over time, improves skill
        public float baseResearchPoints; // Research points generated per turn
        
        // Assignment
        public string assignedProjectId; // null if idle
        public string currentProvinceId; // Where the researcher is located
        
        // State
        public bool isIdle => string.IsNullOrEmpty(assignedProjectId);
        public bool isAssigned => !isIdle;
        
        // Specialization bonus
        public TechCategory? specialization;
        public float specializationBonus; // +20% if researching in specialty
        
        public Researcher(string id, string name, ResearcherType type, FactionType faction)
        {
            this.researcherId = id;
            this.name = name;
            this.type = type;
            this.faction = faction;
            
            InitializeStats();
        }
        
        private void InitializeStats()
        {
            switch (type)
            {
                case ResearcherType.Gentleman:
                    skill = UnityEngine.Random.Range(2, 5);
                    baseResearchPoints = 5f;
                    break;
                case ResearcherType.Scholar:
                    skill = UnityEngine.Random.Range(4, 7);
                    baseResearchPoints = 8f;
                    break;
                case ResearcherType.Scientist:
                    skill = UnityEngine.Random.Range(6, 9);
                    baseResearchPoints = 12f;
                    break;
                case ResearcherType.MilitaryExpert:
                    skill = UnityEngine.Random.Range(5, 8);
                    baseResearchPoints = 9f;
                    specialization = TechCategory.Military;
                    specializationBonus = 0.25f;
                    break;
                case ResearcherType.Economist:
                    skill = UnityEngine.Random.Range(5, 8);
                    baseResearchPoints = 9f;
                    specialization = TechCategory.Economy;
                    specializationBonus = 0.25f;
                    break;
                case ResearcherType.DiplomatExpert:
                    skill = UnityEngine.Random.Range(5, 8);
                    baseResearchPoints = 9f;
                    specialization = TechCategory.Diplomacy;
                    specializationBonus = 0.25f;
                    break;
            }
            
            experience = 0;
        }
        
        /// <summary>
        /// Calculate effective research points for a given project
        /// </summary>
        public float GetResearchPointsPerTurn(TechCategory? projectCategory)
        {
            float points = baseResearchPoints;
            
            // Skill multiplier (1.0x to 2.0x)
            float skillMultiplier = 1f + (skill / 10f);
            points *= skillMultiplier;
            
            // Specialization bonus
            if (specialization.HasValue && projectCategory.HasValue && 
                specialization.Value == projectCategory.Value)
            {
                points *= (1f + specializationBonus);
            }
            
            return points;
        }
        
        /// <summary>
        /// Add experience from completed research
        /// </summary>
        public void AddExperience(int amount)
        {
            experience += amount;
            
            // Skill improvement thresholds
            if (experience > 100 && skill < 8)
            {
                skill++;
                Debug.Log($"[Researcher] {name} skill increased to {skill}!");
            }
        }
        
        /// <summary>
        /// Assign to a research project
        /// </summary>
        public void Assign(string projectId)
        {
            assignedProjectId = projectId;
            Debug.Log($"[Researcher] {name} assigned to project {projectId}");
        }
        
        /// <summary>
        /// Unassign from current project
        /// </summary>
        public void Unassign()
        {
            assignedProjectId = null;
        }
        
        /// <summary>
        /// Get recruitment cost based on type
        /// </summary>
        public static int GetRecruitmentCost(ResearcherType type)
        {
            return type switch
            {
                ResearcherType.Gentleman => 200,
                ResearcherType.Scholar => 400,
                ResearcherType.Scientist => 800,
                ResearcherType.MilitaryExpert => 600,
                ResearcherType.Economist => 600,
                ResearcherType.DiplomatExpert => 600,
                _ => 300
            };
        }
        
        /// <summary>
        /// Get display name for researcher type
        /// </summary>
        public static string GetTypeName(ResearcherType type)
        {
            return type switch
            {
                ResearcherType.Gentleman => "Gentleman",
                ResearcherType.Scholar => "Scholar",
                ResearcherType.Scientist => "Scientist",
                ResearcherType.MilitaryExpert => "Military Expert",
                ResearcherType.Economist => "Economist",
                ResearcherType.DiplomatExpert => "Diplomatic Expert",
                _ => "Researcher"
            };
        }
        
        /// <summary>
        /// Get icon for researcher type
        /// </summary>
        public static string GetTypeIcon(ResearcherType type)
        {
            return type switch
            {
                ResearcherType.Gentleman => "🎩",
                ResearcherType.Scholar => "📚",
                ResearcherType.Scientist => "🔬",
                ResearcherType.MilitaryExpert => "⚔️",
                ResearcherType.Economist => "💰",
                ResearcherType.DiplomatExpert => "📜",
                _ => "👤"
            };
        }
    }

    /// <summary>
    /// Represents an active research project with assigned researchers.
    /// Research progress is measured in points rather than turns.
    /// </summary>
    [Serializable]
    public class ResearchProject
    {
        public string projectId;
        public string techId;
        public string techName;
        public TechCategory category;
        
        // Research requirements
        public float totalResearchPointsRequired;
        public float currentResearchPoints;
        
        // Cost
        public int goldCost;
        public int goldPaid;
        
        // Assignment
        public string provinceId; // Where research is conducted
        public string buildingId; // Academy/University building
        public List<string> assignedResearcherIds = new List<string>();
        
        // Progress tracking
        public int turnsActive;
        public bool isComplete;
        public int turnCompleted;
        
        // Diminishing returns factor
        public const float DIMINISHING_RETURN_FACTOR = 0.75f; // Each additional researcher is 75% as effective
        
        public ResearchProject(string techId, string techName, TechCategory category, 
            float pointsRequired, int goldCost, string provinceId)
        {
            this.projectId = Guid.NewGuid().ToString().Substring(0, 8);
            this.techId = techId;
            this.techName = techName;
            this.category = category;
            this.totalResearchPointsRequired = pointsRequired;
            this.currentResearchPoints = 0f;
            this.goldCost = goldCost;
            this.goldPaid = 0;
            this.provinceId = provinceId;
            this.assignedResearcherIds = new List<string>();
            this.isComplete = false;
            this.turnsActive = 0;
        }
        
        /// <summary>
        /// Calculate research progress for this turn based on assigned researchers
        /// </summary>
        public float CalculateProgress(List<Researcher> researchers)
        {
            float totalPoints = 0f;
            int researcherCount = 0;
            
            foreach (string researcherId in assignedResearcherIds)
            {
                Researcher r = researchers.Find(res => res.researcherId == researcherId);
                if (r == null) continue;
                
                float basePoints = r.GetResearchPointsPerTurn(category);
                
                // Apply diminishing returns
                // 1st researcher: 100%, 2nd: 75%, 3rd: 56%, 4th: 42%, etc.
                float efficiency = Mathf.Pow(DIMINISHING_RETURN_FACTOR, researcherCount);
                totalPoints += basePoints * efficiency;
                
                researcherCount++;
            }
            
            return totalPoints;
        }
        
        /// <summary>
        /// Add research points and check for completion
        /// </summary>
        public bool AddProgress(float points)
        {
            if (isComplete) return false;
            
            currentResearchPoints += points;
            turnsActive++;
            
            if (currentResearchPoints >= totalResearchPointsRequired)
            {
                isComplete = true;
                currentResearchPoints = totalResearchPointsRequired;
                return true; // Completed this turn
            }
            
            return false;
        }
        
        /// <summary>
        /// Get progress percentage (0-100)
        /// </summary>
        public float GetProgressPercent()
        {
            return (currentResearchPoints / totalResearchPointsRequired) * 100f;
        }
        
        /// <summary>
        /// Get estimated turns remaining based on current research rate
        /// </summary>
        public int GetEstimatedTurnsRemaining(List<Researcher> researchers)
        {
            float pointsPerTurn = CalculateProgress(researchers);
            if (pointsPerTurn <= 0) return -1; // No researchers assigned
            
            float remaining = totalResearchPointsRequired - currentResearchPoints;
            return Mathf.CeilToInt(remaining / pointsPerTurn);
        }
        
        /// <summary>
        /// Assign a researcher to this project
        /// </summary>
        public bool AssignResearcher(Researcher researcher)
        {
            if (assignedResearcherIds.Contains(researcher.researcherId)) return false;
            if (isComplete) return false;
            
            assignedResearcherIds.Add(researcher.researcherId);
            researcher.Assign(projectId);
            return true;
        }
        
        /// <summary>
        /// Unassign a researcher from this project
        /// </summary>
        public void UnassignResearcher(Researcher researcher)
        {
            assignedResearcherIds.Remove(researcher.researcherId);
            researcher.Unassign();
        }
        
        /// <summary>
        /// Get number of assigned researchers
        /// </summary>
        public int GetResearcherCount()
        {
            return assignedResearcherIds.Count;
        }
        
        /// <summary>
        /// Calculate total upkeep cost per turn for this project
        /// </summary>
        public int GetUpkeepCostPerTurn()
        {
            // Base upkeep per researcher
            return assignedResearcherIds.Count * 5; // 5 gold per researcher per turn
        }
    }

    /// <summary>
    /// Data for a research building (Academy, University, etc.)
    /// </summary>
    [Serializable]
    public class ResearchBuilding
    {
        public string buildingId;
        public string provinceId;
        public string buildingName;
        public int maxResearchers; // How many researchers can work here
        public float researchEfficiencyBonus; // +0% to +50% research speed
        public TechCategory? categoryFocus; // If set, gives bonus to that category
        public float categoryBonus; // Bonus for focused category
        
        public List<string> assignedResearcherIds = new List<string>();
        
        public ResearchBuilding(string id, string provinceId, string name, int maxSlots, float efficiencyBonus = 0f)
        {
            this.buildingId = id;
            this.provinceId = provinceId;
            this.buildingName = name;
            this.maxResearchers = maxSlots;
            this.researchEfficiencyBonus = efficiencyBonus;
            this.assignedResearcherIds = new List<string>();
        }
        
        public bool HasAvailableSlot()
        {
            return assignedResearcherIds.Count < maxResearchers;
        }
        
        public int GetAvailableSlots()
        {
            return maxResearchers - assignedResearcherIds.Count;
        }
        
        public float GetEfficiencyBonusForCategory(TechCategory category)
        {
            float bonus = researchEfficiencyBonus;
            if (categoryFocus.HasValue && categoryFocus.Value == category)
            {
                bonus += categoryBonus;
            }
            return bonus;
        }
    }
}
