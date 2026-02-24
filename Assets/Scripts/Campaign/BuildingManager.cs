using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Building Manager - Handles global construction queues, research tree, and building management
    /// Similar to Total War: Empire's building system
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        private static BuildingManager _instance;
        public static BuildingManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Don't auto-create during shutdown
                    if (!Application.isPlaying)
                        return null;
                    
                    _instance = FindAnyObjectByType<BuildingManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[BuildingManager]");
                        _instance = go.AddComponent<BuildingManager>();
                        DontDestroyOnLoad(go);
                        Debug.Log("[BuildingManager] Auto-created singleton instance");
                    }
                }
                return _instance;
            }
        }
        
        [Header("Research")]
        public List<ResearchNode> allResearchNodes = new List<ResearchNode>();
        private Dictionary<string, ResearchNode> researchLookup = new Dictionary<string, ResearchNode>();
        
        [Header("Construction")]
        public List<ConstructionQueueItem> globalConstructionQueue = new List<ConstructionQueueItem>();
        private Dictionary<string, List<ConstructionQueueItem>> cityQueues = new Dictionary<string, List<ConstructionQueueItem>>();
        
        [Header("Global Limits")]
        public int maxConcurrentConstruction = 5;
        public int currentActiveConstruction = 0;
        
        private ResearchNode currentResearch = null;
        
        // Events
        public System.Action<string, string> OnConstructionCompleted;
        public System.Action<string> OnResearchCompleted;
        public System.Action OnResearchStarted;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            BuildResearchLookup();
        }
        
        private void BuildResearchLookup()
        {
            // Auto-populate if empty
            if (allResearchNodes.Count == 0)
            {
                Debug.Log("[BuildingManager] No research nodes assigned — auto-initializing...");
                allResearchNodes = BuildingDataInitializer.CreateDefaultResearchTree();
            }
            
            researchLookup.Clear();
            foreach (var node in allResearchNodes)
            {
                if (!string.IsNullOrEmpty(node.nodeId))
                    researchLookup[node.nodeId] = node;
            }
            
            Debug.Log($"[BuildingManager] Loaded {allResearchNodes.Count} research nodes");
        }
        
        // ==================== CONSTRUCTION SYSTEM ====================
        
        /// <summary>
        /// Check if a building can be constructed in a city
        /// </summary>
        public bool CanConstructBuilding(string cityId, string buildingId, out string errorMessage)
        {
            errorMessage = "";
            
            // Get city data
            var city = CampaignManager.Instance?.GetCity(cityId);
            if (city == null)
            {
                errorMessage = "City not found";
                return false;
            }
            
            // Get building data
            var buildingData = BuildingDatabase.Instance?.GetBuilding(buildingId);
            if (buildingData == null)
            {
                errorMessage = "Building not found";
                return false;
            }
            
            // Check if building slot available
            if (city.buildings.Count >= city.maxBuildings && !IsUpgrade(city, buildingId))
            {
                errorMessage = "No building slots available";
                return false;
            }
            
            // Check concurrent construction queue limit
            var currentQueue = GetCityQueue(cityId);
            int maxConcurrent = CityLevelThresholds.GetMaxConcurrentConstruction(city.cityLevel);
            if (currentQueue != null && currentQueue.Count >= maxConcurrent)
            {
                errorMessage = $"Max {maxConcurrent} construction{(maxConcurrent > 1 ? "s" : "")} at a time";
                return false;
            }
            
            // Check capital-gates-subcity: sub-cities can't exceed their capital's level
            var hierarchy = CampaignManager.Instance?.GetCityHierarchy(city.provinceId);
            if (hierarchy != null && hierarchy.Capital != null && hierarchy.Capital.cityId != cityId)
            {
                // This is a sub-city, check if capital is high enough level
                int capitalLevel = hierarchy.Capital.cityData?.cityLevel ?? 1;
                if (city.cityLevel >= capitalLevel)
                {
                    // Sub-city can't have buildings requiring a level higher than capital
                    if (buildingData.requiredCityLevel > capitalLevel)
                    {
                        errorMessage = $"Capital must be Level {buildingData.requiredCityLevel} first";
                        return false;
                    }
                }
            }
            
            // Check city level requirement
            if (city.cityLevel < buildingData.requiredCityLevel)
            {
                errorMessage = $"Requires City Level {buildingData.requiredCityLevel}";
                return false;
            }
            
            // Check prerequisites
            foreach (var prereq in buildingData.prerequisiteBuildings)
            {
                if (!city.HasBuilding(GetBuildingTypeFromId(prereq)))
                {
                    errorMessage = $"Requires: {prereq}";
                    return false;
                }
            }
            
            // Check if already constructing this building
            if (IsBuildingInQueue(cityId, buildingId))
            {
                errorMessage = "Already constructing this building";
                return false;
            }
            
            // Check max level
            var existing = city.buildings.Find(b => b.buildingId == buildingId);
            if (existing != null && existing.level >= buildingData.maxLevel)
            {
                errorMessage = "Maximum level reached";
                return false;
            }
            
            // Check funds
            int cost = buildingData.GetTotalCost(existing != null ? existing.level + 1 : 1);
            if (!CampaignManager.Instance.HasEnoughGold(city.owner, cost))
            {
                errorMessage = "Insufficient funds";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Add a building to construction queue
        /// </summary>
        public bool QueueConstruction(string cityId, string buildingId)
        {
            if (!CanConstructBuilding(cityId, buildingId, out string error))
            {
                Debug.LogWarning($"[BuildingManager] Cannot construct: {error}");
                return false;
            }
            
            var city = CampaignManager.Instance.GetCity(cityId);
            var buildingData = BuildingDatabase.Instance.GetBuilding(buildingId);
            
            // Determine if upgrade or new construction
            var existing = city.buildings.Find(b => b.buildingId == buildingId);
            bool isUpgrade = existing != null;
            int targetLevel = isUpgrade ? existing.level + 1 : 1;
            int cost = buildingData.GetTotalCost(targetLevel);
            int turns = buildingData.GetConstructionTime(targetLevel);
            
            // Deduct funds
            CampaignManager.Instance.SpendGold(city.owner, cost);
            
            // Create queue item
            var queueItem = new ConstructionQueueItem(cityId, buildingId, turns, cost, targetLevel, isUpgrade);
            globalConstructionQueue.Add(queueItem);
            
            // Add to city queue
            if (!cityQueues.ContainsKey(cityId))
                cityQueues[cityId] = new List<ConstructionQueueItem>();
            cityQueues[cityId].Add(queueItem);
            
            // Add placeholder building if new
            if (!isUpgrade)
            {
                city.buildings.Add(new CityBuilding
                {
                    buildingId = buildingId,
                    buildingName = buildingData.displayName,
                    buildingType = GetBuildingTypeFromId(buildingId),
                    level = 0,
                    isConstructed = false,
                    isUpgrading = false,
                    constructionTurnsRemaining = turns
                });
            }
            
            Debug.Log($"[BuildingManager] Queued {buildingData.displayName} Lv.{targetLevel} in {city.cityName} ({turns} turns, {cost} gold)");
            return true;
        }
        
        /// <summary>
        /// Cancel construction and refund
        /// </summary>
        public void CancelConstruction(string queueId)
        {
            var item = globalConstructionQueue.Find(q => q.queueId == queueId);
            if (item == null) return;
            
            // Refund 50% of cost
            var city = CampaignManager.Instance.GetCity(item.cityId);
            if (city != null)
            {
                CampaignManager.Instance.AddGold(city.owner, item.totalCost / 2);
            }
            
            // Remove placeholder building if new construction
            if (!item.isUpgrade)
            {
                var placeholder = city?.buildings.Find(b => b.buildingId == item.buildingId && !b.isConstructed);
                if (placeholder != null)
                    city.buildings.Remove(placeholder);
            }
            
            globalConstructionQueue.Remove(item);
            if (cityQueues.ContainsKey(item.cityId))
                cityQueues[item.cityId].Remove(item);
            
            Debug.Log($"[BuildingManager] Cancelled construction of {item.buildingId}, refunded {item.totalCost / 2} gold");
        }
        
        /// <summary>
        /// Process all construction queues at end of turn
        /// </summary>
        public void ProcessTurn()
        {
            // Process construction queue
            var completed = new List<ConstructionQueueItem>();
            
            foreach (var item in globalConstructionQueue.ToList())
            {
                var city = CampaignManager.Instance?.GetCity(item.cityId);
                if (city == null) continue;
                
                item.turnsRemaining--;
                
                // Update construction turns on building
                var building = city.buildings.Find(b => b.buildingId == item.buildingId);
                if (building != null)
                    building.constructionTurnsRemaining = item.turnsRemaining;
                
                if (item.turnsRemaining <= 0)
                {
                    CompleteConstruction(item);
                    completed.Add(item);
                }
            }
            
            // Remove completed from queue
            foreach (var item in completed)
            {
                globalConstructionQueue.Remove(item);
                if (cityQueues.ContainsKey(item.cityId))
                    cityQueues[item.cityId].Remove(item);
            }
            
            // Process research
            if (currentResearch != null && currentResearch.isResearching)
            {
                currentResearch.turnsRemaining--;
                
                if (currentResearch.turnsRemaining <= 0)
                {
                    CompleteResearch(currentResearch);
                }
            }
        }
        
        private void CompleteConstruction(ConstructionQueueItem item)
        {
            var city = CampaignManager.Instance?.GetCity(item.cityId);
            if (city == null) return;
            
            var building = city.buildings.Find(b => b.buildingId == item.buildingId);
            if (building == null) return;
            
            building.level = item.targetLevel;
            building.isConstructed = true;
            building.isUpgrading = false;
            building.constructionTurnsRemaining = 0;
            
            var buildingData = BuildingDatabase.Instance?.GetBuilding(item.buildingId);
            Debug.Log($"[BuildingManager] Completed: {buildingData?.displayName} Lv.{item.targetLevel} in {city.cityName}");
            
            OnConstructionCompleted?.Invoke(item.cityId, item.buildingId);
        }
        
        // ==================== RESEARCH SYSTEM ====================
        
        public bool CanResearch(string nodeId, out string errorMessage)
        {
            errorMessage = "";
            
            if (!researchLookup.TryGetValue(nodeId, out var node))
            {
                errorMessage = "Research node not found";
                return false;
            }
            
            if (node.isResearched)
            {
                errorMessage = "Already researched";
                return false;
            }
            
            if (currentResearch != null)
            {
                errorMessage = "Research in progress";
                return false;
            }
            
            // Check prerequisites
            foreach (var prereq in node.prerequisiteNodes)
            {
                if (!researchLookup.TryGetValue(prereq, out var prereqNode) || !prereqNode.isResearched)
                {
                    errorMessage = "Prerequisites not met";
                    return false;
                }
            }
            
            // Check faction gold
            var faction = CampaignManager.Instance?.GetPlayerFaction();
            if (faction == null || faction.gold < node.researchCost)
            {
                errorMessage = "Insufficient funds";
                return false;
            }
            
            return true;
        }
        
        public bool StartResearch(string nodeId)
        {
            if (!CanResearch(nodeId, out string error))
            {
                Debug.LogWarning($"[BuildingManager] Cannot research: {error}");
                return false;
            }
            
            var node = researchLookup[nodeId];
            var faction = CampaignManager.Instance.GetPlayerFaction();
            
            // Deduct cost
            faction.gold -= node.researchCost;
            
            // Start research
            node.isResearching = true;
            node.turnsRemaining = node.researchTime;
            currentResearch = node;
            
            Debug.Log($"[BuildingManager] Started research: {node.displayName} ({node.researchTime} turns, {node.researchCost} gold)");
            OnResearchStarted?.Invoke();
            
            return true;
        }
        
        public void CancelResearch()
        {
            if (currentResearch == null || !currentResearch.isResearching) return;
            
            // Refund 50%
            var faction = CampaignManager.Instance?.GetPlayerFaction();
            if (faction != null)
                faction.gold += currentResearch.researchCost / 2;
            
            currentResearch.isResearching = false;
            currentResearch.turnsRemaining = 0;
            currentResearch = null;
        }
        
        private void CompleteResearch(ResearchNode node)
        {
            node.isResearched = true;
            node.isResearching = false;
            node.turnsRemaining = 0;
            currentResearch = null;
            
            Debug.Log($"[BuildingManager] Research completed: {node.displayName}");
            
            // Apply global effects
            var faction = CampaignManager.Instance?.GetPlayerFaction();
            if (faction != null)
            {
                // These would be applied in faction data
            }
            
            OnResearchCompleted?.Invoke(node.nodeId);
        }
        
        // ==================== HELPER METHODS ====================
        
        public List<ConstructionQueueItem> GetCityQueue(string cityId)
        {
            if (cityQueues.TryGetValue(cityId, out var queue))
                return new List<ConstructionQueueItem>(queue);
            return new List<ConstructionQueueItem>();
        }
        
        public bool IsBuildingInQueue(string cityId, string buildingId)
        {
            return globalConstructionQueue.Any(q => q.cityId == cityId && q.buildingId == buildingId);
        }
        
        private bool IsUpgrade(CityData city, string buildingId)
        {
            return city.buildings.Any(b => b.buildingId == buildingId && b.isConstructed);
        }
        
        private BuildingType GetBuildingTypeFromId(string buildingId)
        {
            // Simple conversion - in practice you'd map this properly
            if (System.Enum.TryParse<BuildingType>(buildingId, true, out var type))
                return type;
            return BuildingType.Empty;
        }
        
        public ResearchNode GetCurrentResearch()
        {
            return currentResearch;
        }
        
        public List<ResearchNode> GetAvailableResearch()
        {
            var available = new List<ResearchNode>();
            foreach (var node in allResearchNodes)
            {
                if (!node.isResearched && !node.isResearching && CanResearch(node.nodeId, out _))
                    available.Add(node);
            }
            return available;
        }
        
        public List<ResearchNode> GetResearchedNodes()
        {
            return allResearchNodes.FindAll(n => n.isResearched);
        }
        
        public ResearchNode GetResearchNode(string nodeId)
        {
            if (researchLookup.TryGetValue(nodeId, out var node))
                return node;
            return null;
        }
    }
}
