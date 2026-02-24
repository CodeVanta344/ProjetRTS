using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    [System.Serializable]
    public class CityData
    {
        public string cityId;
        public string cityName;
        public string provinceId;
        public FactionType owner;
        public Vector2 mapPosition;
        
        // City stats
        public PopulationData populationData = new PopulationData(3500, 1250, 250);
        public int maxPopulation = 500000;
        public float publicOrder = 100f;
        
        // City level (1-5)
        public int cityLevel = 1;
        
        // Capital city — unique resources, higher production, larger storage
        public bool isCapital = false;
        
        // Computed total population
        public int population => populationData.Total;
        
        // Industries in this city
        public List<IndustrySlot> industries = new List<IndustrySlot>();
        
        // Buildings
        public List<CityBuilding> buildings = new List<CityBuilding>();
        public int maxBuildings = 6;
        
        // Production queue
        public List<ProductionQueueItem> productionQueue = new List<ProductionQueueItem>();
        
        // Garrison
        public int garrisonSize = 0;
        public int maxGarrison = 1000;
        
        // Resources storage
        public float storedFood = 100f;
        public float storedIron = 50f;
        public float storedGoods = 0f;
        
        public CityData(string id, string name, string provinceId, Vector2 position, FactionType owner)
        {
            this.cityId = id;
            this.cityName = name;
            this.provinceId = provinceId;
            this.mapPosition = position;
            this.owner = owner;
            
            // Initialize with some default industries
            InitializeDefaultIndustries();
            
            // Set initial city level based on starting population
            UpdateCityLevel();
        }
        
        private void InitializeDefaultIndustries()
        {
            // Every city has basic agriculture
            industries.Add(new IndustrySlot(IndustryType.Agriculture, 1));
            industries.Add(new IndustrySlot(IndustryType.Logging, 1));
        }
        
        public void UpdateCity()
        {
            // === Population Growth by Class ===
            if (population < maxPopulation && publicOrder > 50f)
            {
                float orderModifier = (publicOrder - 50f) / 100f;
                
                // Base growth rates per class
                float workerGrowth = 0.03f;
                float bourgeoisGrowth = 0.02f;
                float nobleGrowth = 0.01f;
                
                // Building influence on class growth
                float bourgeoisBonus = 0f;
                float nobleBonus = 0f;
                foreach (var building in buildings)
                {
                    if (!building.isConstructed) continue;
                    switch (building.buildingType)
                    {
                        case BuildingType.Market:
                            bourgeoisBonus += 0.005f * building.level; // Markets attract bourgeois
                            break;
                        case BuildingType.Church:
                            nobleBonus += 0.003f * building.level; // Churches attract nobles
                            break;
                        case BuildingType.University:
                            bourgeoisBonus += 0.003f * building.level;
                            nobleBonus += 0.002f * building.level;
                            break;
                    }
                }
                
                // Apply growth
                int workerGain = Mathf.RoundToInt(populationData.workers * workerGrowth * orderModifier);
                int bourgeoisGain = Mathf.RoundToInt(populationData.bourgeois * (bourgeoisGrowth + bourgeoisBonus) * orderModifier);
                int nobleGain = Mathf.RoundToInt(populationData.nobles * (nobleGrowth + nobleBonus) * orderModifier);
                
                // Ensure minimum growth of 1 per class if population exists
                if (populationData.workers > 0 && workerGain == 0) workerGain = 1;
                if (populationData.bourgeois > 0 && bourgeoisGain == 0) bourgeoisGain = 1;
                if (populationData.nobles > 0 && nobleGain == 0) nobleGain = 1;
                
                populationData.workers += workerGain;
                populationData.bourgeois += bourgeoisGain;
                populationData.nobles += nobleGain;
                
                // Cap total at maxPopulation
                if (population > maxPopulation)
                {
                    float ratio = (float)maxPopulation / population;
                    populationData.workers = Mathf.RoundToInt(populationData.workers * ratio);
                    populationData.bourgeois = Mathf.RoundToInt(populationData.bourgeois * ratio);
                    populationData.nobles = Mathf.RoundToInt(populationData.nobles * ratio);
                }
                
                // Social mobility: small % of workers become bourgeois, bourgeois become nobles
                int workersToBourgeois = Mathf.Max(0, Mathf.RoundToInt(populationData.workers * 0.002f));
                int bourgeoisToNobles = Mathf.Max(0, Mathf.RoundToInt(populationData.bourgeois * 0.001f));
                
                populationData.workers -= workersToBourgeois;
                populationData.bourgeois += workersToBourgeois - bourgeoisToNobles;
                populationData.nobles += bourgeoisToNobles;
            }
            else if (publicOrder <= 50f)
            {
                // Population decline during unrest — workers suffer most
                int decline = Mathf.RoundToInt(populationData.workers * 0.01f);
                populationData.workers = Mathf.Max(100, populationData.workers - decline);
            }
            
            // === City Level Check ===
            UpdateCityLevel();
            
            // === Update Industries ===
            foreach (var industry in industries)
            {
                industry.UpdateProduction();
            }
            
            // === Process Production Queue ===
            ProcessProductionQueue();
            
            // === Consume Food ===
            float foodConsumption = population * 0.01f;
            storedFood -= foodConsumption;
            if (storedFood < 0)
            {
                storedFood = 0;
                publicOrder -= 5f;
            }
            
            // === Public Order from Nobles ===
            // Nobles stabilize public order slightly
            float nobleOrderBonus = (float)populationData.nobles / Mathf.Max(1, population) * 5f;
            publicOrder = Mathf.Clamp(publicOrder + nobleOrderBonus, 0f, 100f);
        }
        
        private void UpdateCityLevel()
        {
            int oldLevel = cityLevel;
            
            if (population >= CityLevelThresholds.METROPOLE)
                cityLevel = 5;
            else if (population >= CityLevelThresholds.GRANDE_VILLE)
                cityLevel = 4;
            else if (population >= CityLevelThresholds.VILLE)
                cityLevel = 3;
            else if (population >= CityLevelThresholds.BOURG)
                cityLevel = 2;
            else
                cityLevel = 1;
            
            // Update max buildings on level up
            maxBuildings = CityLevelThresholds.GetMaxBuildings(cityLevel);
            
            if (cityLevel > oldLevel)
            {
                Debug.Log($"[City] {cityName} leveled up to {CityLevelThresholds.GetLevelName(cityLevel)} (Lv.{cityLevel})! Pop: {population:N0}");
            }
        }
        
        public string CityLevelName => CityLevelThresholds.GetLevelName(cityLevel);
        
        public int NextLevelPopulation => CityLevelThresholds.GetNextThreshold(cityLevel);
        
        // Gold bonus from bourgeois population
        public float BourgeoisGoldBonus => populationData.bourgeois * 0.002f;
        
        // Research bonus from nobles
        public float NobleResearchBonus => populationData.nobles * 0.003f;
        
        private void ProcessProductionQueue()
        {
            if (productionQueue.Count == 0) return;
            
            var item = productionQueue[0];
            item.turnsRemaining--;
            
            if (item.turnsRemaining <= 0)
            {
                // Production complete
                CompleteProduction(item);
                productionQueue.RemoveAt(0);
            }
        }
        
        private void CompleteProduction(ProductionQueueItem item)
        {
            switch (item.itemType)
            {
                case ProductionItemType.Building:
                    var bld = buildings.Find(b => b.buildingId == item.buildingId);
                    if (bld != null)
                    {
                        bld.isConstructed = true;
                        bld.constructionTurnsRemaining = 0;
                        Debug.Log($"[CityData] {cityName}: Building {bld.buildingName} completed!");
                    }
                    break;
                case ProductionItemType.Unit:
                    Debug.Log($"[CityData] {cityName}: Unit {item.unitType} produced!");
                    break;
            }
        }
        
        public bool StartBuildingConstruction(BuildingType type)
        {
            // Check if already building or constructed
            var existing = buildings.Find(b => b.buildingType == type);
            if (existing != null && (existing.isConstructed || existing.isConstructing)) return false;
            
            var building = new CityBuilding
            {
                buildingId = System.Guid.NewGuid().ToString(),
                buildingName = BuildingInfo.GetName(type),
                buildingType = type,
                level = 1,
                isConstructed = false,
                constructionTurnsRemaining = BuildingInfo.GetBuildTime(type)
            };
            
            buildings.Add(building);
            
            productionQueue.Add(new ProductionQueueItem
            {
                itemType = ProductionItemType.Building,
                buildingId = building.buildingId,
                turnsRemaining = building.constructionTurnsRemaining
            });
            
            return true;
        }
        
        public void StartBuildingConstruction(BuildingType type, int cost)
        {
            // MULTIPLAYER CHECK
            if (NapoleonicWars.Network.NetworkLobbyManager.Instance != null && 
                NapoleonicWars.Network.NetworkLobbyManager.Instance.IsConnected)
            {
                var netCampaign = NapoleonicWars.Network.NetworkCampaignManager.Instance;
                if (netCampaign != null && !netCampaign.IsServerProcessingActions)
                {
                    netCampaign.QueueActionServerRpc(new NapoleonicWars.Network.NetworkCampaignAction
                    {
                        faction = owner,
                        actionType = NapoleonicWars.Network.CampaignActionType.BuildBuilding,
                        sourceId = cityId,
                        param1 = (int)type,
                        priority = 50
                    });
                    Debug.Log($"[CityData] Building {type} queued for network turn resolution.");
                    return;
                }
            }

            // Check if already building
            if (buildings.Exists(b => b.buildingType == type && b.isConstructing)) return;
            
            // Check if already constructed
            var existing = buildings.Find(b => b.buildingType == type);
            if (existing != null && existing.isConstructed) return;

            if (!CampaignManager.Instance.SpendGold(owner, cost)) return;

            // Add to queue
            var bld = new CityBuilding { buildingType = type, level = 1, isConstructing = true, turnsToComplete = 3 };
            if (existing != null)
            {
                existing.isConstructing = true;
                existing.turnsToComplete = 3;
            }
            else
            {
                buildings.Add(bld);
            }
            
            Debug.Log($"[CityData] {cityName} started building {type}");
        }

        public bool StartUnitProduction(UnitType type, int cost)
        {
            // MULTIPLAYER CHECK
            if (NapoleonicWars.Network.NetworkLobbyManager.Instance != null && 
                NapoleonicWars.Network.NetworkLobbyManager.Instance.IsConnected)
            {
                var netCampaign = NapoleonicWars.Network.NetworkCampaignManager.Instance;
                if (netCampaign != null && !netCampaign.IsServerProcessingActions)
                {
                    netCampaign.QueueActionServerRpc(new NapoleonicWars.Network.NetworkCampaignAction
                    {
                        faction = owner,
                        actionType = NapoleonicWars.Network.CampaignActionType.RecruitUnit,
                        sourceId = cityId,
                        param1 = (int)type,
                        priority = 60
                    });
                    Debug.Log($"[CityData] Recruitment {type} queued for network turn resolution.");
                    return true;
                }
            }

            // Check if we have required buildings
            if (!CanProduceUnit(type)) return false;

            if (cost > 0 && !CampaignManager.Instance.SpendGold(owner, cost)) return false;
            
            int turns = GetUnitProductionTurns(type);
            
            productionQueue.Add(new ProductionQueueItem
            {
                itemType = ProductionItemType.Unit,
                unitType = type,
                turnsRemaining = turns
            });
            
            Debug.Log($"[CityData] {cityName} queued unit {type}");
            return true;
        }
        
        public bool CanProduceUnit(UnitType type)
        {
            switch (type)
            {
                case UnitType.LineInfantry:
                case UnitType.LightInfantry:
                case UnitType.Grenadier:
                    return buildings.Exists(b => b.buildingType == BuildingType.Barracks && b.isConstructed);
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    return buildings.Exists(b => b.buildingType == BuildingType.Stables && b.isConstructed);
                case UnitType.Artillery:
                    return buildings.Exists(b => b.buildingType == BuildingType.Armory && b.isConstructed);
                default:
                    return false;
            }
        }
        
        private int GetUnitProductionTurns(UnitType type)
        {
            switch (type)
            {
                case UnitType.LineInfantry: return 2;
                case UnitType.LightInfantry: return 2;
                case UnitType.Grenadier: return 3;
                case UnitType.Cavalry: return 3;
                case UnitType.Hussar: return 3;
                case UnitType.Lancer: return 3;
                case UnitType.Artillery: return 4;
                default: return 2;
            }
        }
        
        public void AddIndustry(IndustryType type, int level = 1)
        {
            var existing = industries.Find(i => i.industryType == type);
            if (existing != null)
            {
                existing.level++;
            }
            else
            {
                industries.Add(new IndustrySlot(type, level));
            }
        }

        // Helper methods for AI and Network systems
        public bool HasBuilding(BuildingType type)
        {
            return buildings.Exists(b => b.buildingType == type && b.isConstructed);
        }

        public void QueueBuilding(BuildingType type)
        {
            StartBuildingConstruction(type, BuildingInfo.GetCostGold(type, 1));
        }

        public void QueueUnit(UnitType type)
        {
            StartUnitProduction(type, 0);
        }

        public int GetFortificationLevel()
        {
            // Fortification level is based on city size AND existing fortress building
            int baseFortification = cityLevel switch
            {
                1 => 0,  // Village - no walls
                2 => 1,  // Bourg - basic palisade
                3 => 2,  // Ville - stone walls
                4 => 3,  // Grande Ville - fortified walls
                5 => 4,  // Métropole - massive fortifications
                _ => 0
            };
            
            // Add fortress building bonus
            var fort = buildings.Find(b => b.buildingType == BuildingType.Fortress && b.isConstructed);
            int fortressBonus = fort?.level ?? 0;
            
            return Mathf.Min(baseFortification + fortressBonus, 5); // Max level 5
        }
        
        /// <summary>Returns true if city has defensive walls</summary>
        public bool IsFortified => GetFortificationLevel() > 0;
    }
    
    [System.Serializable]
    public class CityBuilding
    {
        public string buildingId;
        public string buildingName;
        public BuildingType buildingType;
        public int level = 1;
        public bool isConstructed = true;
        public bool isConstructing = false;
        public bool isUpgrading = false;
        public int constructionTurnsRemaining = 0;
        public int turnsToComplete = 0;
        
        // Production modifiers
        public float goldBonus = 0f;
        public float foodBonus = 0f;
        public float ironBonus = 0f;
        public float productionBonus = 0f;
        public float growthBonus = 0f;
        public float publicOrderBonus = 0f;
        
        public BuildingData GetBuildingData()
        {
            return BuildingDatabase.Instance?.GetBuilding(buildingId);
        }
    }
    
    [System.Serializable]
    public class IndustrySlot
    {
        public IndustryType industryType;
        public int level = 1;
        public float efficiency = 1.0f;
        public bool isActive = true;
        
        public IndustrySlot(IndustryType type, int level)
        {
            this.industryType = type;
            this.level = level;
        }
        
        public void UpdateProduction()
        {
            // Industries produce based on their type and level
            // Actual resource generation is handled by the city
        }
        
        public float GetGoldOutput()
        {
            if (!isActive) return 0f;
            
            return industryType switch
            {
                IndustryType.Commerce => level * 20f * efficiency,
                IndustryType.Manufacturing => level * 15f * efficiency,
                IndustryType.Agriculture => level * 2f * efficiency,
                _ => 0f
            };
        }
        
        public float GetFoodOutput()
        {
            if (!isActive) return 0f;
            
            return industryType switch
            {
                IndustryType.Agriculture => level * 25f * efficiency,
                IndustryType.Fishing => level * 20f * efficiency,
                _ => 0f
            };
        }
        
        public float GetIronOutput()
        {
            if (!isActive) return 0f;
            
            return industryType switch
            {
                IndustryType.Mining => level * 15f * efficiency,
                _ => 0f
            };
        }
        
        public float GetWoodOutput()
        {
            if (!isActive) return 0f;
            
            return industryType switch
            {
                IndustryType.Logging => level * 20f * efficiency,
                _ => 0f
            };
        }
        
        public float GetProductionOutput()
        {
            if (!isActive) return 0f;
            
            return industryType switch
            {
                IndustryType.Manufacturing => level * 10f * efficiency,
                IndustryType.Shipbuilding => level * 8f * efficiency,
                _ => 0f
            };
        }
    }
    
    public enum IndustryType
    {
        Agriculture,      // Food production
        Mining,         // Iron production
        Logging,        // Wood production
        Manufacturing,  // Goods and military production
        Commerce,       // Gold income
        Fishing,        // Food (coastal cities)
        Shipbuilding,   // Naval units and trade
        Textile         // Goods production
    }
    
    [System.Serializable]
    public class ProductionQueueItem
    {
        public ProductionItemType itemType;
        public int turnsRemaining;
        
        // For units
        public UnitType unitType;
        
        // For buildings
        public string buildingId;
        
        // For goods
        public float goodsAmount;
    }
    
    public enum ProductionItemType
    {
        Unit,
        Building,
        Goods
    }
    
    // === POPULATION DATA ===
    [System.Serializable]
    public class PopulationData
    {
        public int workers;      // Ouvriers — main workforce, food consumers
        public int bourgeois;    // Bourgeois — gold income, trade
        public int nobles;       // Nobles — public order, research
        
        public int Total => workers + bourgeois + nobles;
        
        public float WorkerRatio => Total > 0 ? (float)workers / Total : 0.7f;
        public float BourgeoisRatio => Total > 0 ? (float)bourgeois / Total : 0.25f;
        public float NobleRatio => Total > 0 ? (float)nobles / Total : 0.05f;
        
        public PopulationData(int workers, int bourgeois, int nobles)
        {
            this.workers = workers;
            this.bourgeois = bourgeois;
            this.nobles = nobles;
        }
        
        /// <summary>Initialize from a total population with default class distribution</summary>
        public static PopulationData FromTotal(int totalPop)
        {
            int w = Mathf.RoundToInt(totalPop * 0.70f);
            int b = Mathf.RoundToInt(totalPop * 0.25f);
            int n = totalPop - w - b; // remainder goes to nobles
            return new PopulationData(w, b, Mathf.Max(n, 1));
        }
    }
    
    // === CITY LEVEL THRESHOLDS ===
    public static class CityLevelThresholds
    {
        public const int VILLAGE = 0;
        public const int BOURG = 10000;
        public const int VILLE = 30000;
        public const int GRANDE_VILLE = 80000;
        public const int METROPOLE = 200000;
        
        public static string GetLevelName(int level)
        {
            return level switch
            {
                1 => "Village",
                2 => "Bourg",
                3 => "Ville",
                4 => "Grande Ville",
                5 => "Métropole",
                _ => "Village"
            };
        }
        
        public static int GetMaxBuildings(int level)
        {
            return level switch
            {
                1 => 4,   // Village - 4 slots
                2 => 5,   // Bourg - 5 slots
                3 => 7,   // Ville - 7 slots
                4 => 9,   // Grande Ville - 9 slots
                5 => 10,  // Métropole - 10 slots
                _ => 4
            };
        }
        
        /// <summary>Max concurrent construction projects based on city level</summary>
        public static int GetMaxConcurrentConstruction(int level)
        {
            return level switch
            {
                1 => 1,   // Village - 1 at a time
                2 => 1,   // Bourg - 1 at a time
                3 => 2,   // Ville - 2 at a time
                4 => 2,   // Grande Ville - 2 at a time
                5 => 2,   // Métropole - 2 at a time
                _ => 1
            };
        }
        
        public static int GetNextThreshold(int level)
        {
            return level switch
            {
                1 => BOURG,
                2 => VILLE,
                3 => GRANDE_VILLE,
                4 => METROPOLE,
                5 => -1, // Max level
                _ => BOURG
            };
        }
        
        /// <summary>Gold income multiplier from city level</summary>
        public static float GetGoldMultiplier(int level)
        {
            return level switch
            {
                1 => 1.0f,
                2 => 1.1f,   // +10%
                3 => 1.2f,   // +20%
                4 => 1.3f,   // +30%
                5 => 1.5f,   // +50%
                _ => 1.0f
            };
        }
        
        /// <summary>Production speed multiplier from city level</summary>
        public static float GetProductionMultiplier(int level)
        {
            return level switch
            {
                1 => 1.0f,
                2 => 1.0f,
                3 => 1.1f,   // +10%
                4 => 1.2f,   // +20%
                5 => 1.3f,   // +30%
                _ => 1.0f
            };
        }
    }
}
