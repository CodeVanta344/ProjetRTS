using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// HoI4-style production system. Military manufactories produce equipment each turn.
    /// Civilian workshops build buildings. Each faction has production lines.
    /// </summary>
    
    [System.Serializable]
    public class ProductionLine
    {
        public string lineId;
        public EquipmentType equipmentType;
        public int assignedFactories = 1;
        public float efficiency = 0.10f;  // Grows 1% per turn up to 100%
        public float totalProduced = 0f;

        public ProductionLine(EquipmentType type, int factories)
        {
            lineId = System.Guid.NewGuid().ToString().Substring(0, 8);
            equipmentType = type;
            assignedFactories = factories;
            efficiency = 0.10f; // Start at 10%
        }

        /// <summary>Units produced per turn for this line</summary>
        public float OutputPerTurn => assignedFactories * GetBaseOutput(equipmentType) * efficiency;

        public static float GetBaseOutput(EquipmentType type) => type switch
        {
            EquipmentType.Muskets => 50f,
            EquipmentType.Bayonets => 60f,
            EquipmentType.Sabres => 40f,
            EquipmentType.CannonsLight => 5f,
            EquipmentType.CannonsHeavy => 3f,
            EquipmentType.CannonsSiege => 1f,
            EquipmentType.Horses => 15f,
            EquipmentType.Uniforms => 80f,
            EquipmentType.Gunpowder => 100f,
            EquipmentType.Cannonballs => 120f,
            _ => 10f
        };

        /// <summary>Advance efficiency by 1 turn (grows logarithmically)</summary>
        public void AdvanceTurn()
        {
            efficiency = Mathf.Min(1.0f, efficiency + 0.01f * (1.1f - efficiency));
        }
    }

    [System.Serializable]
    public class ConstructionProject
    {
        public string projectId;
        public string provinceId;
        public string buildingId;
        public int assignedWorkshops = 1;
        public float progress = 0f;       // 0 to 1
        public float totalRequired;        // Total work units
        public int priority = 0;           // Lower = higher priority

        public ConstructionProject(string province, string building, float totalWork, int workshops)
        {
            projectId = System.Guid.NewGuid().ToString().Substring(0, 8);
            provinceId = province;
            buildingId = building;
            totalRequired = totalWork;
            assignedWorkshops = workshops;
        }

        public float ProgressPerTurn => assignedWorkshops * 10f; // 10 work units per workshop per turn
        public bool IsComplete => progress >= totalRequired;
    }

    public static class ProductionManager
    {
        // Production lines per faction
        private static Dictionary<FactionType, List<ProductionLine>> productionLines = 
            new Dictionary<FactionType, List<ProductionLine>>();

        // Construction projects per faction
        private static Dictionary<FactionType, List<ConstructionProject>> constructionProjects =
            new Dictionary<FactionType, List<ConstructionProject>>();

        public static void Initialize(FactionType faction)
        {
            if (!productionLines.ContainsKey(faction))
                productionLines[faction] = new List<ProductionLine>();
            if (!constructionProjects.ContainsKey(faction))
                constructionProjects[faction] = new List<ConstructionProject>();
        }

        // === PRODUCTION LINES ===

        public static void AddProductionLine(FactionType faction, EquipmentType type, int factories)
        {
            Initialize(faction);
            productionLines[faction].Add(new ProductionLine(type, factories));
        }

        public static void RemoveProductionLine(FactionType faction, string lineId)
        {
            Initialize(faction);
            productionLines[faction].RemoveAll(l => l.lineId == lineId);
        }

        public static List<ProductionLine> GetProductionLines(FactionType faction)
        {
            Initialize(faction);
            return productionLines[faction];
        }

        public static int GetAssignedMilitaryFactories(FactionType faction)
        {
            Initialize(faction);
            int total = 0;
            foreach (var line in productionLines[faction])
                total += line.assignedFactories;
            return total;
        }

        /// <summary>Process one turn of production for a faction</summary>
        public static void ProcessProductionTurn(FactionData faction)
        {
            Initialize(faction.factionType);
            float economyMod = EquipmentSystem.GetEconomyModifier(faction.economyLaw);

            foreach (var line in productionLines[faction.factionType])
            {
                float output = line.OutputPerTurn * economyMod;
                faction.equipment.Add(line.equipmentType, output);
                line.totalProduced += output;
                line.AdvanceTurn();
            }
        }

        // === CONSTRUCTION PROJECTS ===

        public static void AddConstructionProject(FactionType faction, string province, string building, float totalWork, int workshops)
        {
            Initialize(faction);
            constructionProjects[faction].Add(new ConstructionProject(province, building, totalWork, workshops));
        }

        public static List<ConstructionProject> GetConstructionProjects(FactionType faction)
        {
            Initialize(faction);
            return constructionProjects[faction];
        }

        /// <summary>Process one turn of construction. Returns list of completed project IDs.</summary>
        public static List<string> ProcessConstructionTurn(FactionType faction)
        {
            Initialize(faction);
            var completed = new List<string>();

            foreach (var project in constructionProjects[faction])
            {
                project.progress += project.ProgressPerTurn;
                if (project.IsComplete)
                    completed.Add(project.projectId);
            }

            // Remove completed
            constructionProjects[faction].RemoveAll(p => p.IsComplete);
            return completed;
        }

        /// <summary>Create default production lines for a faction (starting game setup)</summary>
        public static void CreateDefaultProductionLines(FactionData faction)
        {
            Initialize(faction.factionType);
            
            // Default: spread military factories across key equipment
            int milFact = faction.militaryFactories;
            if (milFact >= 3)
            {
                AddProductionLine(faction.factionType, EquipmentType.Muskets, Mathf.Max(1, milFact / 3));
                AddProductionLine(faction.factionType, EquipmentType.Gunpowder, Mathf.Max(1, milFact / 3));
                AddProductionLine(faction.factionType, EquipmentType.Uniforms, Mathf.Max(1, milFact - milFact / 3 * 2));
            }
            else if (milFact > 0)
            {
                AddProductionLine(faction.factionType, EquipmentType.Muskets, milFact);
            }
        }

        /// <summary>Get total equipment demand vs supply summary for UI</summary>
        public static Dictionary<EquipmentType, (float stock, float demand, float production)> GetEquipmentSummary(
            FactionData faction, Dictionary<string, ArmyData> armies)
        {
            var summary = new Dictionary<EquipmentType, (float stock, float demand, float production)>();
            var demand = EquipmentSystem.CalculateTotalDemand(faction, armies);
            
            Initialize(faction.factionType);
            var prodByType = new Dictionary<EquipmentType, float>();
            foreach (var line in productionLines[faction.factionType])
            {
                if (!prodByType.ContainsKey(line.equipmentType))
                    prodByType[line.equipmentType] = 0;
                prodByType[line.equipmentType] += line.OutputPerTurn;
            }

            foreach (EquipmentType t in System.Enum.GetValues(typeof(EquipmentType)))
            {
                float s = faction.equipment.Get(t);
                float d = demand.ContainsKey(t) ? demand[t] : 0;
                float p = prodByType.ContainsKey(t) ? prodByType[t] : 0;
                summary[t] = (s, d, p);
            }
            return summary;
        }
    }
}
