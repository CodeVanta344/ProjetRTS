using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    // ==================== SUPPLY WAGON DATA ====================
    /// <summary>
    /// A physical supply wagon that moves from a city to a target army on the map.
    /// Created by the Intendant player to resupply armies in the field.
    /// </summary>
    [System.Serializable]
    public class SupplyWagon
    {
        public string wagonId;
        public FactionType owner;
        
        // Origin
        public string originCityId;
        public string originProvince;
        
        // Target army
        public string targetArmyId;
        
        // Resources carried
        public float food;
        public float gold;
        public float iron;
        public Dictionary<EquipmentType, float> equipment = new Dictionary<EquipmentType, float>();
        
        // Movement
        public Vector3 currentWorldPosition;
        public Vector3 targetWorldPosition;    // Updated each frame to follow army
        public float moveSpeed = 20f;          // Slower than armies (30)
        public bool isMoving = true;
        
        // Status
        public bool isDelivered = false;
        public bool isCaptured = false;
        public bool isActive = true;
        
        // Visual
        public int wagonSize = 1;              // 1=small cart, 2=wagon train, 3=large convoy
        
        public float TotalResourceValue => gold + food * 0.5f + iron * 1.5f + 
            equipment.Values.Sum() * 2f;
        
        public SupplyWagon(string id, string cityId, string province, 
            string armyId, FactionType owner, Vector3 startPos)
        {
            wagonId = id;
            originCityId = cityId;
            originProvince = province;
            targetArmyId = armyId;
            this.owner = owner;
            currentWorldPosition = startPos;
            targetWorldPosition = startPos;
            
            // Wagon size based on total resources (set after loading)
            wagonSize = 1;
        }
        
        public void UpdateWagonSize()
        {
            float val = TotalResourceValue;
            if (val > 500f) wagonSize = 3;
            else if (val > 200f) wagonSize = 2;
            else wagonSize = 1;
        }
    }
    
    // ==================== SUPPLY WAGON SYSTEM ====================
    /// <summary>
    /// Manages supply wagons that physically move from cities to armies.
    /// The Intendant (Player 2) creates wagons at cities and assigns them to armies.
    /// Wagons move on the map and deliver supplies on arrival.
    /// </summary>
    public static class SupplyWagonSystem
    {
        // All active wagons
        private static List<SupplyWagon> wagons = new List<SupplyWagon>();
        private static int nextId = 0;
        
        // Events
        public static System.Action<SupplyWagon> OnWagonCreated;
        public static System.Action<SupplyWagon> OnWagonDelivered;
        public static System.Action<SupplyWagon> OnWagonCaptured;
        
        // ==================== CREATION ====================
        
        /// <summary>
        /// Create a supply wagon at a city, targeting an army.
        /// Resources are deducted from the city/faction immediately.
        /// </summary>
        public static SupplyWagon CreateWagon(
            CityData city, ArmyData targetArmy, FactionType owner,
            float gold, float food, float iron,
            Dictionary<EquipmentType, float> equipment,
            Vector3 cityWorldPos)
        {
            if (city == null || targetArmy == null) return null;
            
            // Validate resources
            FactionData faction = CampaignManager.Instance?.GetFaction(owner);
            if (faction == null) return null;
            
            // Check faction can afford
            if (faction.gold < gold) { Debug.LogWarning("[SupplyWagon] Not enough gold"); return null; }
            if (faction.food < food) { Debug.LogWarning("[SupplyWagon] Not enough food"); return null; }
            if (faction.iron < iron) { Debug.LogWarning("[SupplyWagon] Not enough iron"); return null; }
            
            // Deduct resources
            faction.gold -= gold;
            faction.food -= food;
            faction.iron -= iron;
            
            // Deduct equipment
            if (equipment != null && faction.equipment != null)
            {
                foreach (var kvp in equipment)
                    faction.equipment.Consume(kvp.Key, kvp.Value);
            }
            
            // Create wagon
            string id = $"wagon_{nextId++}";
            var wagon = new SupplyWagon(id, city.cityId, city.provinceId, 
                targetArmy.armyId, owner, cityWorldPos)
            {
                gold = gold,
                food = food,
                iron = iron,
                equipment = equipment ?? new Dictionary<EquipmentType, float>()
            };
            wagon.UpdateWagonSize();
            
            // Setup cost (small gold fee for the cart itself)
            float cartCost = 20f + wagon.wagonSize * 10f;
            faction.gold -= cartCost;
            
            wagons.Add(wagon);
            
            OnWagonCreated?.Invoke(wagon);
            Debug.Log($"[SupplyWagon] 🛒 {id} created at {city.cityName} → army {targetArmy.armyName} (value: {wagon.TotalResourceValue:F0}, size: {wagon.wagonSize})");
            
            return wagon;
        }
        
        /// <summary>
        /// Quick create: send food + basic supplies to an army.
        /// Uses default amounts based on army size.
        /// </summary>
        public static SupplyWagon QuickResupply(CityData city, ArmyData targetArmy, 
            FactionType owner, Vector3 cityWorldPos)
        {
            int armySize = targetArmy.TotalSoldiers;
            float foodNeeded = armySize * 0.5f;  // 0.5 food per soldier
            float goldNeeded = armySize * 0.1f;   // 0.1 gold per soldier (pay)
            
            return CreateWagon(city, targetArmy, owner, goldNeeded, foodNeeded, 0f, null, cityWorldPos);
        }
        
        // ==================== MOVEMENT & DELIVERY ====================
        
        /// <summary>
        /// Update all wagon positions. Called each frame from CampaignMap3D.
        /// Wagons follow their target army's current position.
        /// </summary>
        public static void UpdateWagons(Dictionary<string, ArmyData> armies, 
            Dictionary<string, ArmyMapMarker> armyMarkers, float deltaTime)
        {
            foreach (var wagon in wagons)
            {
                if (!wagon.isActive || wagon.isDelivered) continue;
                
                // Find target army position
                if (!armies.ContainsKey(wagon.targetArmyId))
                {
                    // Target army no longer exists — wagon lost
                    wagon.isActive = false;
                    Debug.Log($"[SupplyWagon] {wagon.wagonId} lost — target army destroyed");
                    continue;
                }
                
                // Get army marker position for world coords
                Vector3 armyPos = wagon.targetWorldPosition; // Fallback
                if (armyMarkers.TryGetValue(wagon.targetArmyId, out var marker) && marker != null)
                {
                    armyPos = marker.transform.position;
                }
                wagon.targetWorldPosition = armyPos;
                
                // Move toward army
                float dist = Vector3.Distance(
                    new Vector3(wagon.currentWorldPosition.x, 0, wagon.currentWorldPosition.z),
                    new Vector3(armyPos.x, 0, armyPos.z));
                
                if (dist < 50f)
                {
                    // Arrived — deliver supplies
                    DeliverToArmy(wagon, armies[wagon.targetArmyId]);
                }
                else
                {
                    // Move toward target
                    wagon.currentWorldPosition = Vector3.MoveTowards(
                        wagon.currentWorldPosition, armyPos, deltaTime * wagon.moveSpeed);
                }
            }
            
            // Cleanup delivered/captured wagons
            wagons.RemoveAll(w => !w.isActive && (w.isDelivered || w.isCaptured));
        }
        
        /// <summary>
        /// Deliver wagon contents to the target army.
        /// </summary>
        private static void DeliverToArmy(SupplyWagon wagon, ArmyData army)
        {
            wagon.isDelivered = true;
            wagon.isActive = false;
            
            // Gold and iron go to faction reserves
            FactionData faction = CampaignManager.Instance?.GetFaction(wagon.owner);
            if (faction != null)
            {
                faction.gold += wagon.gold;
                faction.iron += wagon.iron;
                
                // Deliver equipment to faction stockpile
                if (faction.equipment != null)
                {
                    foreach (var kvp in wagon.equipment)
                        faction.equipment.Add(kvp.Key, kvp.Value);
                }
            }
            
            // FOOD goes directly to army as rations
            float rationsDelivered = wagon.food / Mathf.Max(1f, army.TotalSoldiers * 0.01f);
            army.ReceiveSupply(rationsDelivered);
            
            // Boost army organization and reduce fatigue
            army.organization = Mathf.Min(army.maxOrganization, army.organization + 30f);
            army.fatigue = Mathf.Max(0f, army.fatigue - 0.3f);
            army.turnsCampaigning = Mathf.Max(0, army.turnsCampaigning - 2);
            
            // Reinforce regiments with available manpower
            float foodPerSoldier = 0.3f;
            int soldiersFromFood = Mathf.FloorToInt(wagon.food / foodPerSoldier);
            foreach (var reg in army.regiments)
            {
                if (soldiersFromFood <= 0) break;
                int missing = reg.maxSize - reg.currentSize;
                if (missing > 0)
                {
                    int reinforced = Mathf.Min(missing, soldiersFromFood);
                    reg.currentSize += reinforced;
                    soldiersFromFood -= reinforced;
                }
            }
            
            // Build depot at army's location if near a friendly province
            if (!string.IsNullOrEmpty(army.currentProvinceId))
            {
                SupplyLineSystem.BuildDepot(army.faction, army.currentProvinceId);
            }
            
            OnWagonDelivered?.Invoke(wagon);
            Debug.Log($"[SupplyWagon] ✅ {wagon.wagonId} DELIVERED to {army.armyName}! " +
                $"(+{rationsDelivered:F1} rations, +{wagon.gold:F0} gold, +{wagon.iron:F0} iron, " +
                $"+30 org, -0.3 fatigue, rations now: {army.currentRations:F1}/{army.maxRations})");
        }
        
        // ==================== CAPTURE ====================
        
        /// <summary>
        /// Check if any enemy army is near a wagon — if so, capture it.
        /// Called during update.
        /// </summary>
        public static void CheckCaptures(Dictionary<string, ArmyData> armies,
            Dictionary<string, ArmyMapMarker> armyMarkers)
        {
            foreach (var wagon in wagons)
            {
                if (!wagon.isActive) continue;
                
                foreach (var kvp in armyMarkers)
                {
                    var marker = kvp.Value;
                    if (marker == null || marker.ArmyData == null) continue;
                    if (marker.ArmyData.faction == wagon.owner) continue; // Same faction
                    
                    float dist = Vector3.Distance(
                        new Vector3(wagon.currentWorldPosition.x, 0, wagon.currentWorldPosition.z),
                        new Vector3(marker.transform.position.x, 0, marker.transform.position.z));
                    
                    if (dist < 100f)
                    {
                        // Captured by enemy!
                        wagon.isCaptured = true;
                        wagon.isActive = false;
                        
                        // Transfer resources to captor faction
                        FactionData captor = CampaignManager.Instance?.GetFaction(marker.ArmyData.faction);
                        if (captor != null)
                        {
                            captor.gold += wagon.gold;
                            captor.food += wagon.food;
                            captor.iron += wagon.iron;
                        }
                        
                        OnWagonCaptured?.Invoke(wagon);
                        Debug.Log($"[SupplyWagon] ❌ {wagon.wagonId} CAPTURED by {marker.ArmyData.faction}! " +
                            $"(value: {wagon.TotalResourceValue:F0})");
                        break;
                    }
                }
            }
        }
        
        // ==================== QUERIES ====================
        
        public static List<SupplyWagon> GetAllWagons() => wagons.ToList();
        
        public static List<SupplyWagon> GetWagonsForFaction(FactionType faction) =>
            wagons.Where(w => w.owner == faction && w.isActive).ToList();
        
        public static List<SupplyWagon> GetWagonsForArmy(string armyId) =>
            wagons.Where(w => w.targetArmyId == armyId && w.isActive).ToList();
        
        public static int GetActiveWagonCount(FactionType faction) =>
            wagons.Count(w => w.owner == faction && w.isActive);
        
        public static float GetTotalValueInTransit(FactionType faction) =>
            wagons.Where(w => w.owner == faction && w.isActive).Sum(w => w.TotalResourceValue);
        
        /// <summary>Clear all wagons (e.g. on campaign restart)</summary>
        public static void Clear()
        {
            wagons.Clear();
            nextId = 0;
        }
    }
}
