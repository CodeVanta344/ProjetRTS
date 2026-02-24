using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Data;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Building Data ScriptableObject - Defines all building types and their properties
    /// Similar to Total War: Empire building system
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingData", menuName = "Napoleonic Wars/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("Basic Info")]
        public string buildingId;
        public string displayName;
        public string description;
        public Sprite icon;
        public BuildingCategory category;
        
        [Header("Construction")]
        public int baseCost = 1000;
        public int costPerLevel = 500;
        public int baseConstructionTime = 2; // turns
        public int maxLevel = 3;
        
        [Header("Requirements")]
        public int requiredCityLevel = 1;
        public List<string> prerequisiteBuildings = new List<string>(); // buildingIds required
        public bool requiresCoastal = false;
        public bool requiresResource = false;
        public IndustryType requiredResourceType;
        
        [Header("Economic Effects")]
        public int goldIncomePerLevel = 0;
        public int foodProductionPerLevel = 0;
        public int ironProductionPerLevel = 0;
        public int woodProductionPerLevel = 0;
        public int tradeIncomePerLevel = 0;
        
        [Header("Population Effects")]
        public int populationGrowthPerLevel = 0;
        public int publicOrderPerLevel = 0;
        public int attractsBourgeoisPerLevel = 0;
        public int attractsNoblesPerLevel = 0;
        
        [Header("Military Effects")]
        public int recruitmentSlotsPerLevel = 0; // Max units queued
        public float recruitmentSpeedBonus = 0f; // 0.1 = 10% faster
        public int garrisonBonusPerLevel = 0;
        public int defenseBonusPerLevel = 0;
        public bool enablesUnitType = false;
        public UnitType unlockedUnitType;
        
        [Header("Research Effects")]
        public float researchSpeedBonus = 0f; // 0.1 = 10% faster
        public List<string> unlockedTechnologies = new List<string>();
        
        [Header("Chain Bonuses")]
        // Bonuses when multiple buildings of same chain exist in province
        public bool isPartOfChain = false;
        public string chainId;
        public float chainBonusPerBuilding = 0.05f; // 5% per building in chain
        
        public int GetTotalCost(int level)
        {
            return baseCost + (costPerLevel * (level - 1));
        }
        
        public int GetConstructionTime(int level)
        {
            return baseConstructionTime + (level - 1);
        }
        
        public string GetEffectsDescription(int level)
        {
            var effects = new List<string>();
            
            if (goldIncomePerLevel > 0) effects.Add($"+{goldIncomePerLevel * level} Gold/turn");
            if (foodProductionPerLevel > 0) effects.Add($"+{foodProductionPerLevel * level} Food/turn");
            if (ironProductionPerLevel > 0) effects.Add($"+{ironProductionPerLevel * level} Iron/turn");
            if (woodProductionPerLevel > 0) effects.Add($"+{woodProductionPerLevel * level} Wood/turn");
            if (populationGrowthPerLevel > 0) effects.Add($"+{populationGrowthPerLevel * level}% Growth");
            if (publicOrderPerLevel > 0) effects.Add($"+{publicOrderPerLevel * level} Public Order");
            if (garrisonBonusPerLevel > 0) effects.Add($"+{garrisonBonusPerLevel * level} Garrison");
            if (defenseBonusPerLevel > 0) effects.Add($"+{defenseBonusPerLevel * level} Defense");
            if (recruitmentSpeedBonus > 0) effects.Add($"+{recruitmentSpeedBonus * 100}% Recruitment Speed");
            if (researchSpeedBonus > 0) effects.Add($"+{researchSpeedBonus * 100}% Research Speed");
            
            return string.Join("\n", effects);
        }
    }
    
    public enum BuildingCategory
    {
        Government,     // Administration, tax collection
        Economy,        // Farms, mines, workshops (renamed from Economic)
        Military,       // Barracks, stables, fortifications
        Infrastructure, // Roads, ports, markets
        Religion,       // Churches, mosques (renamed from Religious)
        Academic,       // Universities, libraries
        Coastal         // Ports, shipyards (requires coastal city)
    }
    
    /// <summary>
    /// Active building instance in a city
    /// </summary>
    [System.Serializable]
    public class BuildingInstance
    {
        public string instanceId;
        public string buildingId; // Reference to BuildingData
        public int level = 1;
        public bool isConstructed = false;
        public int constructionTurnsRemaining = 0;
        public bool isUpgrading = false;
        
        // Current effects (calculated from level)
        public int currentGoldIncome => GetBuildingData()?.goldIncomePerLevel * level ?? 0;
        public int currentFoodProduction => GetBuildingData()?.foodProductionPerLevel * level ?? 0;
        public int currentDefense => GetBuildingData()?.defenseBonusPerLevel * level ?? 0;
        
        public BuildingData GetBuildingData()
        {
            return BuildingDatabase.Instance?.GetBuilding(buildingId);
        }
        
        public string GetDisplayName()
        {
            var data = GetBuildingData();
            if (data == null) return "Unknown";
            return $"{data.displayName} (Lv.{level})";
        }
    }
    
    /// <summary>
    /// Global building database - loaded at runtime
    /// </summary>
    public class BuildingDatabase : MonoBehaviour
    {
        private static BuildingDatabase _instance;
        public static BuildingDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Don't auto-create during shutdown
                    if (!Application.isPlaying)
                        return null;
                    
                    _instance = FindAnyObjectByType<BuildingDatabase>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[BuildingDatabase]");
                        _instance = go.AddComponent<BuildingDatabase>();
                        DontDestroyOnLoad(go);
                        Debug.Log("[BuildingDatabase] Auto-created singleton instance");
                    }
                }
                return _instance;
            }
        }
        
        [SerializeField] private List<BuildingData> allBuildings = new List<BuildingData>();
        private Dictionary<string, BuildingData> buildingLookup = new Dictionary<string, BuildingData>();
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            BuildLookup();
        }
        
        private void BuildLookup()
        {
            // Auto-populate if empty (no ScriptableObjects assigned in Inspector)
            if (allBuildings.Count == 0)
            {
                Debug.Log("[BuildingDatabase] No buildings assigned — auto-initializing from BuildingDataInitializer...");
                allBuildings = BuildingDataInitializer.CreateDefaultBuildings();
            }
            
            buildingLookup.Clear();
            foreach (var building in allBuildings)
            {
                if (!string.IsNullOrEmpty(building.buildingId))
                    buildingLookup[building.buildingId] = building;
            }
            
            Debug.Log($"[BuildingDatabase] Loaded {allBuildings.Count} buildings across {System.Enum.GetValues(typeof(BuildingCategory)).Length} categories");
        }
        
        public BuildingData GetBuilding(string buildingId)
        {
            if (buildingLookup.TryGetValue(buildingId, out var building))
                return building;
            return null;
        }
        
        public List<BuildingData> GetBuildingsByCategory(BuildingCategory category)
        {
            return allBuildings.FindAll(b => b.category == category);
        }
        
        public List<BuildingData> GetAllBuildings()
        {
            return new List<BuildingData>(allBuildings);
        }
        
        public void RegisterBuilding(BuildingData building)
        {
            if (!allBuildings.Contains(building))
            {
                allBuildings.Add(building);
                buildingLookup[building.buildingId] = building;
            }
        }
    }
    
    /// <summary>
    /// Research node for technology tree
    /// </summary>
    [CreateAssetMenu(fileName = "ResearchNode", menuName = "Napoleonic Wars/Research Node")]
    public class ResearchNode : ScriptableObject
    {
        public string nodeId;
        public string displayName;
        public string description;
        public Sprite icon;
        public ResearchCategory category;
        
        [Header("Costs")]
        public int researchCost = 500; // Gold cost
        public int researchTime = 3; // Turns
        
        [Header("Requirements")]
        public List<string> prerequisiteNodes = new List<string>();
        public int requiredEra = 1; // Tech era level
        
        [Header("Unlocks")]
        public List<string> unlockedBuildings = new List<string>(); // buildingIds
        public List<string> unlockedUnits = new List<string>();
        public List<string> unlockedTechnologies = new List<string>();
        
        [Header("Effects")]
        public float globalIncomeBonus = 0f;
        public float globalRecruitmentBonus = 0f;
        public float globalResearchBonus = 0f;
        
        [System.NonSerialized] public bool isResearched = false;
        [System.NonSerialized] public bool isResearching = false;
        [System.NonSerialized] public int turnsRemaining = 0;
    }
    
    public enum ResearchCategory
    {
        Military,       // Weapons, tactics, recruitment
        Economic,       // Trade, agriculture, industry
        Industrial,     // Manufacturing, mining
        Naval,          // Ships, naval warfare
        Philosophical   // Public order, government
    }
    
    /// <summary>
    /// Construction queue item for a specific city
    /// </summary>
    [System.Serializable]
    public class ConstructionQueueItem
    {
        public string queueId;
        public string cityId;
        public string buildingId;
        public int targetLevel; // For upgrades
        public int turnsRemaining;
        public int totalCost;
        public bool isUpgrade;
        
        public ConstructionQueueItem(string cityId, string buildingId, int turns, int cost, int level = 1, bool upgrade = false)
        {
            this.queueId = System.Guid.NewGuid().ToString();
            this.cityId = cityId;
            this.buildingId = buildingId;
            this.turnsRemaining = turns;
            this.totalCost = cost;
            this.targetLevel = level;
            this.isUpgrade = upgrade;
        }
    }
}
