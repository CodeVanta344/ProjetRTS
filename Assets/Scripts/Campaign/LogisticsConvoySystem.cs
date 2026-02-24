using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    // ==================== LOGISTICS CONVOY ====================
    /// <summary>
    /// A single convoy transporting resources between two cities.
    /// Moves 1 province per turn along a BFS-computed route.
    /// </summary>
    [System.Serializable]
    public class LogisticsConvoy
    {
        public string convoyId;
        public string originCityId;
        public string destinationCityId;
        public FactionType owner;
        
        // Resources being transported
        public float gold;
        public float food;
        public float iron;
        public Dictionary<EquipmentType, float> equipment = new Dictionary<EquipmentType, float>();
        
        // Route
        public List<string> routeProvinces = new List<string>(); // province IDs along the path
        public int currentStep;       // index in routeProvinces
        public int totalSteps;        // routeProvinces.Count
        public float progressInStep;  // 0-1 for smooth animation between provinces
        
        // Status
        public bool isActive = true;
        public bool isDelivered = false;
        public bool isCaptured = false;
        public bool isAlliedConvoy = false;
        public FactionType allyFaction;
        
        // Costs
        public float maintenanceCostPerTurn;
        
        // Visual
        public string currentProvinceId => routeProvinces != null && currentStep < routeProvinces.Count 
            ? routeProvinces[currentStep] : "";
        public string nextProvinceId => routeProvinces != null && currentStep + 1 < routeProvinces.Count 
            ? routeProvinces[currentStep + 1] : currentProvinceId;
        
        public float TotalResourceValue => gold + food * 0.5f + iron * 1.5f + 
            equipment.Values.Sum() * 2f;
        
        public LogisticsConvoy(string id, string from, string to, FactionType owner)
        {
            convoyId = id;
            originCityId = from;
            destinationCityId = to;
            this.owner = owner;
            currentStep = 0;
            progressInStep = 0f;
        }
    }
    
    // ==================== LOGISTICS ROUTE ====================
    /// <summary>
    /// A permanent supply chain between two cities that automatically dispatches convoys.
    /// Can be upgraded to increase capacity and speed.
    /// </summary>
    [System.Serializable]
    public class LogisticsRoute
    {
        public string routeId;
        public string fromCityId;
        public string toCityId;
        public FactionType owner;
        
        // Route properties
        public int level = 1;              // 1-3
        public float maxCapacityPerTurn;   // Max resource units per turn
        public float maintenanceCost;      // Gold per turn
        public bool isActive = true;
        public List<string> routePath = new List<string>(); // Cached province path
        
        // Auto-dispatch settings
        public float autoGoldPerTurn;
        public float autoFoodPerTurn;
        public float autoIronPerTurn;
        public Dictionary<EquipmentType, float> autoEquipmentPerTurn = new Dictionary<EquipmentType, float>();
        
        // Allied route
        public bool isAlliedRoute = false;
        public FactionType allyFaction;
        public string agreementId;
        
        // Level-based stats
        public static float GetCapacity(int level) => level switch
        {
            1 => 100f,   // Basic cart road
            2 => 250f,   // Improved road
            3 => 500f,   // Major supply highway
            _ => 100f
        };
        
        public static float GetMaintenance(int level, int routeLength) => level switch
        {
            1 => 5f + routeLength * 2f,      // 5g base + 2g/province
            2 => 8f + routeLength * 3f,      // More maintained
            3 => 12f + routeLength * 4f,     // Heavy infrastructure
            _ => 5f + routeLength * 2f
        };
        
        public static int GetUpgradeCostGold(int currentLevel) => currentLevel switch
        {
            1 => 200,   // Upgrade to Lv.2
            2 => 500,   // Upgrade to Lv.3
            _ => 0
        };
        
        public static int GetUpgradeCostIron(int currentLevel) => currentLevel switch
        {
            1 => 50,    // Upgrade to Lv.2
            2 => 150,   // Upgrade to Lv.3
            _ => 0
        };
        
        public LogisticsRoute(string id, string from, string to, FactionType owner, List<string> path)
        {
            routeId = id;
            fromCityId = from;
            toCityId = to;
            this.owner = owner;
            routePath = path;
            level = 1;
            maxCapacityPerTurn = GetCapacity(1);
            maintenanceCost = GetMaintenance(1, path.Count);
        }
        
        public bool Upgrade(FactionData faction)
        {
            if (level >= 3) return false;
            
            int goldCost = GetUpgradeCostGold(level);
            int ironCost = GetUpgradeCostIron(level);
            
            if (faction.gold < goldCost || faction.iron < ironCost) return false;
            
            faction.gold -= goldCost;
            faction.iron -= ironCost;
            
            level++;
            maxCapacityPerTurn = GetCapacity(level);
            maintenanceCost = GetMaintenance(level, routePath.Count);
            
            Debug.Log($"[Logistics] Route {routeId} upgraded to Lv.{level} (capacity: {maxCapacityPerTurn})");
            return true;
        }
    }
    
    // ==================== ALLIED LOGISTICS AGREEMENT ====================
    [System.Serializable]
    public class AlliedLogisticsAgreement
    {
        public string agreementId;
        public FactionType proposer;
        public FactionType recipient;
        public AgreementStatus status = AgreementStatus.Pending;
        
        // Resources offered
        public float goldPerTurn;
        public float foodPerTurn;
        public float ironPerTurn;
        public Dictionary<EquipmentType, float> equipmentPerTurn = new Dictionary<EquipmentType, float>();
        
        // Route info
        public string fromCityId;  // Proposer's city
        public string toCityId;    // Recipient's city
        
        // Duration
        public int turnsRemaining = -1;   // -1 = indefinite
        public int turnsActive = 0;
        
        public enum AgreementStatus { Pending, Active, Rejected, Cancelled, Expired }
    }
    
    // ==================== LOGISTICS CONVOY SYSTEM ====================
    /// <summary>
    /// Manages all logistics convoys, routes, and allied supply agreements.
    /// Integrates with CampaignManager.ProcessTurn() for per-turn updates.
    /// </summary>
    public static class LogisticsConvoySystem
    {
        // All active convoys by faction
        private static Dictionary<FactionType, List<LogisticsConvoy>> convoys = new Dictionary<FactionType, List<LogisticsConvoy>>();
        
        // All permanent routes by faction
        private static Dictionary<FactionType, List<LogisticsRoute>> routes = new Dictionary<FactionType, List<LogisticsRoute>>();
        
        // Allied agreements
        private static List<AlliedLogisticsAgreement> agreements = new List<AlliedLogisticsAgreement>();
        
        // Counter for unique IDs
        private static int nextConvoyId = 0;
        private static int nextRouteId = 0;
        private static int nextAgreementId = 0;
        
        // ==================== INITIALIZATION ====================
        
        public static void Initialize()
        {
            convoys.Clear();
            routes.Clear();
            agreements.Clear();
            nextConvoyId = 0;
            nextRouteId = 0;
            nextAgreementId = 0;
            
            foreach (FactionType ft in System.Enum.GetValues(typeof(FactionType)))
            {
                convoys[ft] = new List<LogisticsConvoy>();
                routes[ft] = new List<LogisticsRoute>();
            }
            
            Debug.Log("[LogisticsConvoySystem] Initialized");
        }
        
        // ==================== CONVOY CREATION ====================
        
        /// <summary>
        /// Create a one-time convoy to transport resources from one city to another.
        /// Resources are deducted from the origin city immediately.
        /// </summary>
        public static LogisticsConvoy CreateConvoy(
            CityData originCity, CityData destCity, FactionType owner,
            float gold, float food, float iron,
            Dictionary<EquipmentType, float> equipment,
            Dictionary<string, ProvinceData> provinces)
        {
            if (originCity == null || destCity == null) return null;
            
            // Find path between cities via BFS
            List<string> path = FindPath(originCity.provinceId, destCity.provinceId, owner, provinces);
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning($"[Logistics] No path found from {originCity.cityName} to {destCity.cityName}");
                return null;
            }
            
            // Deduct resources from origin city
            originCity.storedFood -= food;
            originCity.storedIron -= iron;
            
            // Deduct from faction gold
            FactionData faction = CampaignManager.Instance?.GetFaction(owner);
            if (faction != null)
            {
                faction.gold -= gold;
                
                // Deduct equipment from faction stockpile
                if (equipment != null && faction.equipment != null)
                {
                    foreach (var kvp in equipment)
                        faction.equipment.Consume(kvp.Key, kvp.Value);
                }
            }
            
            string id = $"convoy_{nextConvoyId++}";
            var convoy = new LogisticsConvoy(id, originCity.cityId, destCity.cityId, owner)
            {
                gold = gold,
                food = food,
                iron = iron,
                equipment = equipment ?? new Dictionary<EquipmentType, float>(),
                routeProvinces = path,
                totalSteps = path.Count,
                currentStep = 0,
                progressInStep = 0f,
                maintenanceCostPerTurn = 5f + path.Count * 2f
            };
            
            if (!convoys.ContainsKey(owner))
                convoys[owner] = new List<LogisticsConvoy>();
            convoys[owner].Add(convoy);
            
            Debug.Log($"[Logistics] Convoy {id}: {originCity.cityName} → {destCity.cityName} ({path.Count} provinces, {convoy.TotalResourceValue:F0} value)");
            return convoy;
        }
        
        /// <summary>
        /// Create an allied convoy (resources go to another faction's city).
        /// </summary>
        public static LogisticsConvoy CreateAlliedConvoy(
            CityData originCity, CityData destCity, 
            FactionType owner, FactionType ally,
            float gold, float food, float iron,
            Dictionary<EquipmentType, float> equipment,
            Dictionary<string, ProvinceData> provinces)
        {
            var convoy = CreateConvoy(originCity, destCity, owner, gold, food, iron, equipment, provinces);
            if (convoy != null)
            {
                convoy.isAlliedConvoy = true;
                convoy.allyFaction = ally;
                convoy.maintenanceCostPerTurn *= 1.5f; // 50% more expensive for cross-border
            }
            return convoy;
        }
        
        // ==================== ROUTE MANAGEMENT ====================
        
        /// <summary>
        /// Create a permanent logistics route between two cities.
        /// </summary>
        public static LogisticsRoute CreateRoute(
            CityData fromCity, CityData toCity, FactionType owner,
            Dictionary<string, ProvinceData> provinces)
        {
            if (fromCity == null || toCity == null) return null;
            
            // Check for duplicate
            if (routes.ContainsKey(owner))
            {
                var existing = routes[owner].Find(r => 
                    r.fromCityId == fromCity.cityId && r.toCityId == toCity.cityId);
                if (existing != null)
                {
                    Debug.LogWarning($"[Logistics] Route already exists: {fromCity.cityName} → {toCity.cityName}");
                    return existing;
                }
            }
            
            List<string> path = FindPath(fromCity.provinceId, toCity.provinceId, owner, provinces);
            if (path == null || path.Count == 0) return null;
            
            // Initial setup cost
            FactionData faction = CampaignManager.Instance?.GetFaction(owner);
            int setupCost = 100 + path.Count * 20; // 100g base + 20g per province
            if (faction == null || faction.gold < setupCost) return null;
            faction.gold -= setupCost;
            
            string id = $"route_{nextRouteId++}";
            var route = new LogisticsRoute(id, fromCity.cityId, toCity.cityId, owner, path);
            
            if (!routes.ContainsKey(owner))
                routes[owner] = new List<LogisticsRoute>();
            routes[owner].Add(route);
            
            Debug.Log($"[Logistics] Route {id}: {fromCity.cityName} → {toCity.cityName} (Lv.{route.level}, {path.Count} provinces, setup: {setupCost}g)");
            return route;
        }
        
        /// <summary>
        /// Remove a logistics route.
        /// </summary>
        public static void RemoveRoute(string routeId, FactionType owner)
        {
            if (!routes.ContainsKey(owner)) return;
            routes[owner].RemoveAll(r => r.routeId == routeId);
            Debug.Log($"[Logistics] Route {routeId} removed");
        }
        
        // ==================== ALLIED AGREEMENTS ====================
        
        /// <summary>
        /// Propose a logistics agreement to an ally.
        /// </summary>
        public static AlliedLogisticsAgreement ProposeAgreement(
            FactionType proposer, FactionType recipient,
            string fromCityId, string toCityId,
            float gold, float food, float iron,
            Dictionary<EquipmentType, float> equipment = null,
            int duration = -1)
        {
            string id = $"agreement_{nextAgreementId++}";
            var agreement = new AlliedLogisticsAgreement
            {
                agreementId = id,
                proposer = proposer,
                recipient = recipient,
                fromCityId = fromCityId,
                toCityId = toCityId,
                goldPerTurn = gold,
                foodPerTurn = food,
                ironPerTurn = iron,
                equipmentPerTurn = equipment ?? new Dictionary<EquipmentType, float>(),
                turnsRemaining = duration,
                status = AlliedLogisticsAgreement.AgreementStatus.Pending
            };
            
            agreements.Add(agreement);
            Debug.Log($"[Logistics] Agreement {id} proposed: {proposer} → {recipient}");
            return agreement;
        }
        
        /// <summary>
        /// Accept or reject an agreement.
        /// </summary>
        public static void RespondToAgreement(string agreementId, bool accept, 
            Dictionary<string, ProvinceData> provinces)
        {
            var agreement = agreements.Find(a => a.agreementId == agreementId);
            if (agreement == null) return;
            
            if (accept)
            {
                agreement.status = AlliedLogisticsAgreement.AgreementStatus.Active;
                
                // Create the allied route
                CityData fromCity = CampaignManager.Instance?.GetCity(agreement.fromCityId);
                CityData toCity = CampaignManager.Instance?.GetCity(agreement.toCityId);
                
                if (fromCity != null && toCity != null)
                {
                    var route = CreateRoute(fromCity, toCity, agreement.proposer, provinces);
                    if (route != null)
                    {
                        route.isAlliedRoute = true;
                        route.allyFaction = agreement.recipient;
                        route.agreementId = agreementId;
                        route.autoGoldPerTurn = agreement.goldPerTurn;
                        route.autoFoodPerTurn = agreement.foodPerTurn;
                        route.autoIronPerTurn = agreement.ironPerTurn;
                        route.autoEquipmentPerTurn = agreement.equipmentPerTurn;
                    }
                }
                
                Debug.Log($"[Logistics] Agreement {agreementId} accepted");
            }
            else
            {
                agreement.status = AlliedLogisticsAgreement.AgreementStatus.Rejected;
                Debug.Log($"[Logistics] Agreement {agreementId} rejected");
            }
        }
        
        /// <summary>
        /// Cancel all agreements and routes with a specific faction (e.g., when alliance breaks).
        /// </summary>
        public static void CancelAllAgreements(FactionType faction1, FactionType faction2)
        {
            foreach (var agreement in agreements)
            {
                if ((agreement.proposer == faction1 && agreement.recipient == faction2) ||
                    (agreement.proposer == faction2 && agreement.recipient == faction1))
                {
                    agreement.status = AlliedLogisticsAgreement.AgreementStatus.Cancelled;
                }
            }
            
            // Remove allied routes
            foreach (var kvp in routes)
            {
                kvp.Value.RemoveAll(r => r.isAlliedRoute && 
                    (r.allyFaction == faction1 || r.allyFaction == faction2));
            }
            
            // Cancel allied convoys in transit (resources lost!)
            foreach (var kvp in convoys)
            {
                foreach (var convoy in kvp.Value)
                {
                    if (convoy.isAlliedConvoy && 
                        (convoy.allyFaction == faction1 || convoy.allyFaction == faction2))
                    {
                        convoy.isActive = false;
                        convoy.isCaptured = true; // Resources lost
                    }
                }
            }
            
            Debug.Log($"[Logistics] All agreements between {faction1} and {faction2} cancelled");
        }
        
        // ==================== TURN PROCESSING ====================
        
        /// <summary>
        /// Process all convoys and routes for one turn.
        /// Called from CampaignManager.ProcessTurn().
        /// </summary>
        public static void ProcessTurn(Dictionary<string, ProvinceData> provinces)
        {
            foreach (var kvp in convoys)
            {
                FactionType faction = kvp.Key;
                FactionData factionData = CampaignManager.Instance?.GetFaction(faction);
                
                var activeConvoys = kvp.Value.Where(c => c.isActive && !c.isDelivered).ToList();
                
                foreach (var convoy in activeConvoys)
                {
                    // === Pay maintenance ===
                    if (factionData != null)
                    {
                        factionData.gold -= convoy.maintenanceCostPerTurn;
                        if (factionData.gold < -100f)
                        {
                            // Bankrupt — convoy abandoned
                            convoy.isActive = false;
                            Debug.Log($"[Logistics] Convoy {convoy.convoyId} abandoned (bankrupt)");
                            continue;
                        }
                    }
                    
                    // === Check for enemy interception ===
                    if (convoy.currentStep < convoy.routeProvinces.Count)
                    {
                        string currentProv = convoy.routeProvinces[convoy.currentStep];
                        if (provinces.ContainsKey(currentProv))
                        {
                            ProvinceData prov = provinces[currentProv];
                            // Convoy captured if passing through enemy territory
                            if (prov.owner != faction && prov.owner != convoy.allyFaction)
                            {
                                convoy.isCaptured = true;
                                convoy.isActive = false;
                                
                                // Transfer resources to captor
                                FactionData captor = CampaignManager.Instance?.GetFaction(prov.owner);
                                if (captor != null)
                                {
                                    captor.gold += convoy.gold;
                                    captor.food += convoy.food;
                                    captor.iron += convoy.iron;
                                    if (captor.equipment != null)
                                    {
                                        foreach (var eq in convoy.equipment)
                                            captor.equipment.Add(eq.Key, eq.Value);
                                    }
                                }
                                
                                Debug.Log($"[Logistics] Convoy {convoy.convoyId} CAPTURED by {prov.owner} at {currentProv}! (value: {convoy.TotalResourceValue:F0})");
                                continue;
                            }
                        }
                    }
                    
                    // === Advance convoy ===
                    convoy.currentStep++;
                    convoy.progressInStep = 0f;
                    
                    // === Check if arrived ===
                    if (convoy.currentStep >= convoy.totalSteps)
                    {
                        DeliverConvoy(convoy);
                    }
                }
                
                // Clean up delivered/captured convoys (keep for 1 turn for visual feedback)
                kvp.Value.RemoveAll(c => !c.isActive && (c.isDelivered || c.isCaptured));
            }
            
            // === Process permanent routes — auto-dispatch convoys ===
            foreach (var kvp in routes)
            {
                FactionType faction = kvp.Key;
                FactionData factionData = CampaignManager.Instance?.GetFaction(faction);
                
                foreach (var route in kvp.Value)
                {
                    if (!route.isActive) continue;
                    
                    // Pay route maintenance
                    if (factionData != null)
                    {
                        factionData.gold -= route.maintenanceCost;
                        if (factionData.gold < -200f)
                        {
                            route.isActive = false;
                            Debug.Log($"[Logistics] Route {route.routeId} suspended (bankrupt)");
                            continue;
                        }
                    }
                    
                    // Auto-dispatch if route has auto-settings
                    float totalAuto = route.autoGoldPerTurn + route.autoFoodPerTurn + route.autoIronPerTurn;
                    if (totalAuto > 0 && totalAuto <= route.maxCapacityPerTurn)
                    {
                        CityData fromCity = CampaignManager.Instance?.GetCity(route.fromCityId);
                        CityData toCity = CampaignManager.Instance?.GetCity(route.toCityId);
                        
                        if (fromCity != null && toCity != null)
                        {
                            // Check if origin has enough resources
                            bool hasResources = true;
                            if (factionData != null && factionData.gold < route.autoGoldPerTurn) hasResources = false;
                            if (fromCity.storedFood < route.autoFoodPerTurn) hasResources = false;
                            if (fromCity.storedIron < route.autoIronPerTurn) hasResources = false;
                            
                            if (hasResources)
                            {
                                CreateConvoy(fromCity, toCity, faction,
                                    route.autoGoldPerTurn, route.autoFoodPerTurn, route.autoIronPerTurn,
                                    route.autoEquipmentPerTurn, provinces);
                            }
                        }
                    }
                }
            }
            
            // === Process agreements ===
            foreach (var agreement in agreements)
            {
                if (agreement.status != AlliedLogisticsAgreement.AgreementStatus.Active) continue;
                
                agreement.turnsActive++;
                if (agreement.turnsRemaining > 0)
                {
                    agreement.turnsRemaining--;
                    if (agreement.turnsRemaining <= 0)
                    {
                        agreement.status = AlliedLogisticsAgreement.AgreementStatus.Expired;
                        // Remove associated route
                        if (routes.ContainsKey(agreement.proposer))
                            routes[agreement.proposer].RemoveAll(r => r.agreementId == agreement.agreementId);
                    }
                }
            }
        }
        
        /// <summary>
        /// Deliver a convoy's resources to the destination city.
        /// </summary>
        private static void DeliverConvoy(LogisticsConvoy convoy)
        {
            convoy.isDelivered = true;
            convoy.isActive = false;
            
            CityData destCity = CampaignManager.Instance?.GetCity(convoy.destinationCityId);
            if (destCity == null) return;
            
            // Deliver resources to city
            destCity.storedFood += convoy.food;
            destCity.storedIron += convoy.iron;
            destCity.storedGoods += convoy.gold; // Gold goes to city goods (local economy)
            
            // Deliver gold to faction
            FactionType targetFaction = convoy.isAlliedConvoy ? convoy.allyFaction : convoy.owner;
            FactionData targetFac = CampaignManager.Instance?.GetFaction(targetFaction);
            if (targetFac != null)
            {
                targetFac.gold += convoy.gold;
                
                // Deliver equipment
                if (targetFac.equipment != null)
                {
                    foreach (var eq in convoy.equipment)
                        targetFac.equipment.Add(eq.Key, eq.Value);
                }
            }
            
            Debug.Log($"[Logistics] Convoy {convoy.convoyId} DELIVERED to {destCity.cityName} (value: {convoy.TotalResourceValue:F0})");
        }
        
        // ==================== PATHFINDING ====================
        
        /// <summary>
        /// BFS pathfinding through friendly/neutral provinces.
        /// </summary>
        public static List<string> FindPath(string fromProvince, string toProvince, 
            FactionType owner, Dictionary<string, ProvinceData> provinces)
        {
            if (fromProvince == toProvince) return new List<string> { fromProvince };
            if (!provinces.ContainsKey(fromProvince) || !provinces.ContainsKey(toProvince)) return null;
            
            var queue = new Queue<string>();
            var visited = new HashSet<string>();
            var parent = new Dictionary<string, string>();
            
            queue.Enqueue(fromProvince);
            visited.Add(fromProvince);
            
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                
                if (current == toProvince)
                {
                    // Reconstruct path
                    var path = new List<string>();
                    string node = toProvince;
                    while (node != null)
                    {
                        path.Insert(0, node);
                        parent.TryGetValue(node, out node);
                        if (node == fromProvince) { path.Insert(0, fromProvince); break; }
                    }
                    return path;
                }
                
                ProvinceData prov = provinces[current];
                if (prov.neighborIds == null) continue;
                
                foreach (string neighbor in prov.neighborIds)
                {
                    if (visited.Contains(neighbor)) continue;
                    if (!provinces.ContainsKey(neighbor)) continue;
                    
                    // Allow passage through friendly territory or destination
                    ProvinceData neighborProv = provinces[neighbor];
                    if (neighborProv.owner == owner || neighbor == toProvince)
                    {
                        visited.Add(neighbor);
                        parent[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            Debug.LogWarning($"[Logistics] No path found: {fromProvince} → {toProvince}");
            return null;
        }
        
        // ==================== QUERIES ====================
        
        public static List<LogisticsConvoy> GetConvoys(FactionType faction)
        {
            return convoys.ContainsKey(faction) ? convoys[faction] : new List<LogisticsConvoy>();
        }
        
        public static List<LogisticsConvoy> GetAllActiveConvoys()
        {
            var all = new List<LogisticsConvoy>();
            foreach (var kvp in convoys)
                all.AddRange(kvp.Value.Where(c => c.isActive));
            return all;
        }
        
        public static List<LogisticsRoute> GetRoutes(FactionType faction)
        {
            return routes.ContainsKey(faction) ? routes[faction] : new List<LogisticsRoute>();
        }
        
        public static List<LogisticsRoute> GetAllRoutes()
        {
            var all = new List<LogisticsRoute>();
            foreach (var kvp in routes)
                all.AddRange(kvp.Value);
            return all;
        }
        
        public static List<AlliedLogisticsAgreement> GetAgreements(FactionType faction)
        {
            return agreements.Where(a => a.proposer == faction || a.recipient == faction).ToList();
        }
        
        public static List<AlliedLogisticsAgreement> GetPendingAgreements(FactionType faction)
        {
            return agreements.Where(a => a.recipient == faction && 
                a.status == AlliedLogisticsAgreement.AgreementStatus.Pending).ToList();
        }
        
        /// <summary>Get total maintenance cost for all convoys and routes of a faction.</summary>
        public static float GetTotalMaintenanceCost(FactionType faction)
        {
            float total = 0f;
            if (convoys.ContainsKey(faction))
                total += convoys[faction].Where(c => c.isActive).Sum(c => c.maintenanceCostPerTurn);
            if (routes.ContainsKey(faction))
                total += routes[faction].Where(r => r.isActive).Sum(r => r.maintenanceCost);
            return total;
        }
        
        /// <summary>Get convoy count for a faction.</summary>
        public static int GetConvoyCount(FactionType faction)
        {
            return convoys.ContainsKey(faction) ? convoys[faction].Count(c => c.isActive) : 0;
        }
        
        /// <summary>Get route count for a faction.</summary>
        public static int GetRouteCount(FactionType faction)
        {
            return routes.ContainsKey(faction) ? routes[faction].Count(r => r.isActive) : 0;
        }
    }
}
