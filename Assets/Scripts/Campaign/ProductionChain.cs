using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    [System.Serializable]
    public class ProductionChain
    {
        public string chainId;
        public string chainName;
        public List<ProductionStep> steps = new List<ProductionStep>();
        
        public float GetEfficiency(CityData city)
        {
            float efficiency = 1f;
            foreach (var step in steps)
            {
                if (!step.IsComplete(city))
                {
                    efficiency *= 0.5f;
                }
            }
            return efficiency;
        }
    }
    
    [System.Serializable]
    public class ProductionStep
    {
        public string stepName;
        public ResourceType inputResource;
        public float inputAmount;
        public ResourceType outputResource;
        public float outputAmount;
        public BuildingType requiredBuilding;
        public int requiredBuildingLevel = 1;
        
        public bool IsComplete(CityData city)
        {
            var building = city.buildings.Find(b => b.buildingType == requiredBuilding && b.isConstructed);
            return building != null && building.level >= requiredBuildingLevel;
        }
        
        public bool CanProcess(CityData city, float availableInput)
        {
            return IsComplete(city) && availableInput >= inputAmount;
        }
    }
    
    public enum ResourceType
    {
        Gold,
        Food,
        Iron,
        Wood,
        Goods,
        Weapons,
        Ammunition,
        Textiles,
        Horses,
        NavalSupplies,
        Coal,
        Saltpetre
    }
    
    public class ProductionChainManager
    {
        private static ProductionChainManager _instance;
        public static ProductionChainManager Instance => _instance ??= new ProductionChainManager();
        
        private Dictionary<string, ProductionChain> chains = new Dictionary<string, ProductionChain>();
        private Dictionary<ResourceType, ResourceInfo> resourceDatabase = new Dictionary<ResourceType, ResourceInfo>();
        
        public void Initialize()
        {
            SetupResourceDatabase();
            SetupDefaultChains();
        }
        
        private void SetupResourceDatabase()
        {
            resourceDatabase[ResourceType.Gold] = new ResourceInfo("Gold", "Currency for trade and construction", 1f);
            resourceDatabase[ResourceType.Food] = new ResourceInfo("Food", "Essential for population growth", 0.5f);
            resourceDatabase[ResourceType.Iron] = new ResourceInfo("Iron", "Used for weapons and construction", 2f);
            resourceDatabase[ResourceType.Wood] = new ResourceInfo("Wood", "Construction material", 0.8f);
            resourceDatabase[ResourceType.Goods] = new ResourceInfo("Goods", "Manufactured products for trade", 1.5f);
            resourceDatabase[ResourceType.Weapons] = new ResourceInfo("Weapons", "Military equipment", 3f);
            resourceDatabase[ResourceType.Ammunition] = new ResourceInfo("Ammunition", "Powder and shot", 2.5f);
            resourceDatabase[ResourceType.Textiles] = new ResourceInfo("Textiles", "Cloth and fabric", 1f);
            resourceDatabase[ResourceType.Horses] = new ResourceInfo("Horses", "Cavalry and transport", 4f);
            resourceDatabase[ResourceType.NavalSupplies] = new ResourceInfo("Naval Supplies", "Ship building materials", 3f);
        }
        
        private void SetupDefaultChains()
        {
            // Weapons production chain
            chains["weapons"] = new ProductionChain
            {
                chainId = "weapons",
                chainName = "Weapons Manufacturing",
                steps = new List<ProductionStep>
                {
                    new ProductionStep
                    {
                        stepName = "Iron Mining",
                        inputResource = ResourceType.Gold,
                        inputAmount = 10f,
                        outputResource = ResourceType.Iron,
                        outputAmount = 10f,
                        requiredBuilding = BuildingType.Mine,
                        requiredBuildingLevel = 1
                    },
                    new ProductionStep
                    {
                        stepName = "Wood Harvesting",
                        inputResource = ResourceType.Gold,
                        inputAmount = 5f,
                        outputResource = ResourceType.Wood,
                        outputAmount = 15f,
                        requiredBuilding = BuildingType.Farm, // Using farm as placeholder for logging
                        requiredBuildingLevel = 1
                    },
                    new ProductionStep
                    {
                        stepName = "Weapon Smithing",
                        inputResource = ResourceType.Iron,
                        inputAmount = 5f,
                        outputResource = ResourceType.Weapons,
                        outputAmount = 5f,
                        requiredBuilding = BuildingType.Armory,
                        requiredBuildingLevel = 1
                    },
                    new ProductionStep
                    {
                        stepName = "Ammunition Production",
                        inputResource = ResourceType.Iron,
                        inputAmount = 3f,
                        outputResource = ResourceType.Ammunition,
                        outputAmount = 10f,
                        requiredBuilding = BuildingType.Armory,
                        requiredBuildingLevel = 2
                    }
                }
            };
            
            // Cavalry production chain
            chains["cavalry"] = new ProductionChain
            {
                chainId = "cavalry",
                chainName = "Cavalry Production",
                steps = new List<ProductionStep>
                {
                    new ProductionStep
                    {
                        stepName = "Horse Breeding",
                        inputResource = ResourceType.Food,
                        inputAmount = 20f,
                        outputResource = ResourceType.Horses,
                        outputAmount = 5f,
                        requiredBuilding = BuildingType.Stables,
                        requiredBuildingLevel = 1
                    },
                    new ProductionStep
                    {
                        stepName = "Equipment Crafting",
                        inputResource = ResourceType.Iron,
                        inputAmount = 3f,
                        outputResource = ResourceType.Weapons,
                        outputAmount = 3f,
                        requiredBuilding = BuildingType.Armory,
                        requiredBuildingLevel = 1
                    }
                }
            };
            
            // Naval production chain
            chains["naval"] = new ProductionChain
            {
                chainId = "naval",
                chainName = "Naval Construction",
                steps = new List<ProductionStep>
                {
                    new ProductionStep
                    {
                        stepName = "Timber Processing",
                        inputResource = ResourceType.Wood,
                        inputAmount = 30f,
                        outputResource = ResourceType.NavalSupplies,
                        outputAmount = 10f,
                        requiredBuilding = BuildingType.Armory,
                        requiredBuildingLevel = 1
                    },
                    new ProductionStep
                    {
                        stepName = "Iron Forging",
                        inputResource = ResourceType.Iron,
                        inputAmount = 10f,
                        outputResource = ResourceType.Weapons,
                        outputAmount = 5f,
                        requiredBuilding = BuildingType.Mine,
                        requiredBuildingLevel = 2
                    }
                }
            };
            
            // Textile production chain
            chains["textile"] = new ProductionChain
            {
                chainId = "textile",
                chainName = "Textile Manufacturing",
                steps = new List<ProductionStep>
                {
                    new ProductionStep
                    {
                        stepName = "Raw Material Collection",
                        inputResource = ResourceType.Food,
                        inputAmount = 10f,
                        outputResource = ResourceType.Goods,
                        outputAmount = 5f,
                        requiredBuilding = BuildingType.Farm,
                        requiredBuildingLevel = 2
                    },
                    new ProductionStep
                    {
                        stepName = "Textile Production",
                        inputResource = ResourceType.Goods,
                        inputAmount = 5f,
                        outputResource = ResourceType.Textiles,
                        outputAmount = 5f,
                        requiredBuilding = BuildingType.Market,
                        requiredBuildingLevel = 2
                    }
                }
            };
        }
        
        public ProductionChain GetChain(string chainId)
        {
            return chains.ContainsKey(chainId) ? chains[chainId] : null;
        }
        
        public List<ProductionChain> GetAllChains()
        {
            return new List<ProductionChain>(chains.Values);
        }
        
        public ResourceInfo GetResourceInfo(ResourceType type)
        {
            return resourceDatabase.ContainsKey(type) ? resourceDatabase[type] : null;
        }
        
        public float CalculateCityProduction(CityData city, ResourceType resourceType)
        {
            float total = 0f;
            
            foreach (var industry in city.industries)
            {
                total += resourceType switch
                {
                    ResourceType.Gold => industry.GetGoldOutput(),
                    ResourceType.Food => industry.GetFoodOutput(),
                    ResourceType.Iron => industry.GetIronOutput(),
                    ResourceType.Wood => industry.GetWoodOutput(),
                    ResourceType.Goods => industry.GetProductionOutput(),
                    _ => 0f
                };
            }
            
            // Apply building bonuses
            foreach (var building in city.buildings)
            {
                if (!building.isConstructed) continue;
                
                total += resourceType switch
                {
                    ResourceType.Gold => building.goldBonus * building.level,
                    ResourceType.Food => building.foodBonus * building.level,
                    ResourceType.Iron => building.ironBonus * building.level,
                    _ => 0f
                };
            }
            
            return total;
        }
        
        public Dictionary<ResourceType, float> GetCityDailyProduction(CityData city)
        {
            var production = new Dictionary<ResourceType, float>();
            
            production[ResourceType.Gold] = CalculateCityProduction(city, ResourceType.Gold);
            production[ResourceType.Food] = CalculateCityProduction(city, ResourceType.Food);
            production[ResourceType.Iron] = CalculateCityProduction(city, ResourceType.Iron);
            production[ResourceType.Wood] = CalculateCityProduction(city, ResourceType.Wood);
            production[ResourceType.Goods] = CalculateCityProduction(city, ResourceType.Goods);
            
            return production;
        }
        
        public bool CanProduceUnit(CityData city, UnitType unitType)
        {
            switch (unitType)
            {
                case UnitType.LineInfantry:
                case UnitType.LightInfantry:
                case UnitType.Grenadier:
                    return city.buildings.Exists(b => b.buildingType == BuildingType.Barracks && b.isConstructed);
                    
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    bool hasStables = city.buildings.Exists(b => b.buildingType == BuildingType.Stables && b.isConstructed);
                    bool hasHorses = city.storedFood >= 50; // Simplified: use food as proxy for horses
                    return hasStables && hasHorses;
                    
                case UnitType.Artillery:
                    bool hasArmory = city.buildings.Exists(b => b.buildingType == BuildingType.Armory && b.isConstructed);
                    bool hasIron = city.storedIron >= 20;
                    return hasArmory && hasIron;
                    
                default:
                    return false;
            }
        }
        
        public int GetUnitProductionCost(UnitType unitType)
        {
            return unitType switch
            {
                UnitType.LineInfantry => 200,
                UnitType.LightInfantry => 250,
                UnitType.Grenadier => 350,
                UnitType.Cavalry => 400,
                UnitType.Hussar => 450,
                UnitType.Lancer => 400,
                UnitType.Artillery => 600,
                _ => 200
            };
        }
        
        public Dictionary<ResourceType, float> GetUnitResourceCost(UnitType unitType)
        {
            var costs = new Dictionary<ResourceType, float>();
            
            switch (unitType)
            {
                case UnitType.LineInfantry:
                    costs[ResourceType.Gold] = 200f;
                    costs[ResourceType.Weapons] = 10f;
                    break;
                case UnitType.LightInfantry:
                    costs[ResourceType.Gold] = 250f;
                    costs[ResourceType.Weapons] = 8f;
                    break;
                case UnitType.Grenadier:
                    costs[ResourceType.Gold] = 350f;
                    costs[ResourceType.Weapons] = 15f;
                    costs[ResourceType.Ammunition] = 10f;
                    break;
                case UnitType.Cavalry:
                    costs[ResourceType.Gold] = 400f;
                    costs[ResourceType.Horses] = 20f;
                    costs[ResourceType.Weapons] = 12f;
                    break;
                case UnitType.Hussar:
                    costs[ResourceType.Gold] = 450f;
                    costs[ResourceType.Horses] = 25f;
                    costs[ResourceType.Weapons] = 10f;
                    break;
                case UnitType.Lancer:
                    costs[ResourceType.Gold] = 400f;
                    costs[ResourceType.Horses] = 20f;
                    costs[ResourceType.Weapons] = 15f;
                    break;
                case UnitType.Artillery:
                    costs[ResourceType.Gold] = 600f;
                    costs[ResourceType.Iron] = 30f;
                    costs[ResourceType.Wood] = 20f;
                    break;
            }
            
            return costs;
        }
    }
    
    public class ResourceInfo
    {
        public string name;
        public string description;
        public float baseValue;
        
        public ResourceInfo(string name, string description, float baseValue)
        {
            this.name = name;
            this.description = description;
            this.baseValue = baseValue;
        }
    }
}
