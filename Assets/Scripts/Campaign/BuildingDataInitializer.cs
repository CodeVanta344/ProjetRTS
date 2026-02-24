using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Creates default building data assets for the game
    /// Run this in the editor to generate ScriptableObjects
    /// </summary>
    public static class BuildingDataInitializer
    {
        private static Sprite LoadBuildingIcon(string buildingId)
        {
            // Convert buildingId to icon filename: "town_hall" -> "bld_town_hall"
            string resourcePath = $"UI/Cards/Buildings/bld_{buildingId}";
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return null;
        }
        
        public static List<BuildingData> CreateDefaultBuildings()
        {
            var buildings = new List<BuildingData>();
            
            // ===================== GOVERNMENT BUILDINGS =====================
            
            // Town Hall - Increases city level capacity and gold income
            var townHall = ScriptableObject.CreateInstance<BuildingData>();
            townHall.buildingId = "town_hall";
            townHall.displayName = "Town Hall";
            townHall.description = "Administrative center that improves tax collection and city management.";
            townHall.category = BuildingCategory.Government;
            townHall.baseCost = 1500;
            townHall.baseConstructionTime = 3;
            townHall.maxLevel = 3;
            townHall.goldIncomePerLevel = 50;
            townHall.publicOrderPerLevel = 5;
            buildings.Add(townHall);
            
            // Governor's Residence - Unlocks advanced buildings
            var governorResidence = ScriptableObject.CreateInstance<BuildingData>();
            governorResidence.buildingId = "governor_residence";
            governorResidence.displayName = "Governor's Residence";
            governorResidence.description = "Residence of the provincial governor. Enables advanced construction projects.";
            governorResidence.category = BuildingCategory.Government;
            governorResidence.baseCost = 2500;
            governorResidence.baseConstructionTime = 4;
            governorResidence.maxLevel = 2;
            governorResidence.requiredCityLevel = 3;
            governorResidence.goldIncomePerLevel = 30;
            governorResidence.attractsNoblesPerLevel = 50;
            buildings.Add(governorResidence);
            
            // ===================== ECONOMIC BUILDINGS =====================
            
            // Farm - Food production
            var farm = ScriptableObject.CreateInstance<BuildingData>();
            farm.buildingId = "farm";
            farm.displayName = "Farm Estates";
            farm.description = "Agricultural estates that produce food to sustain the population and army.";
            farm.category = BuildingCategory.Economy;
            farm.baseCost = 400;
            farm.baseConstructionTime = 1;
            farm.maxLevel = 5;
            farm.foodProductionPerLevel = 30;
            farm.populationGrowthPerLevel = 2;
            buildings.Add(farm);
            
            // Mine - Iron production
            var mine = ScriptableObject.CreateInstance<BuildingData>();
            mine.buildingId = "mine";
            mine.displayName = "Iron Mine";
            mine.description = "Extracts iron ore for weapons and construction. Essential for military production.";
            mine.category = BuildingCategory.Economy;
            mine.baseCost = 600;
            mine.baseConstructionTime = 2;
            mine.maxLevel = 4;
            mine.requiresResource = true;
            mine.requiredResourceType = IndustryType.Mining;
            mine.ironProductionPerLevel = 20;
            mine.goldIncomePerLevel = 10;
            buildings.Add(mine);
            
            // Logging Camp - Wood production
            var logging = ScriptableObject.CreateInstance<BuildingData>();
            logging.buildingId = "logging";
            logging.displayName = "Logging Camp";
            logging.description = "Produces timber for construction and shipbuilding.";
            logging.category = BuildingCategory.Economy;
            logging.baseCost = 300;
            logging.baseConstructionTime = 1;
            logging.maxLevel = 4;
            logging.woodProductionPerLevel = 25;
            buildings.Add(logging);
            
            // Workshop - Manufacturing
            var workshop = ScriptableObject.CreateInstance<BuildingData>();
            workshop.buildingId = "workshop";
            workshop.displayName = "Crafstmens' Workshop";
            workshop.description = "Manufactures trade goods and improves economic productivity.";
            workshop.category = BuildingCategory.Economy;
            workshop.baseCost = 800;
            workshop.baseConstructionTime = 2;
            workshop.maxLevel = 3;
            workshop.requiredCityLevel = 2;
            workshop.goldIncomePerLevel = 40;
            workshop.tradeIncomePerLevel = 20;
            buildings.Add(workshop);
            
            // Market - Trade income
            var market = ScriptableObject.CreateInstance<BuildingData>();
            market.buildingId = "market";
            market.displayName = "Market Square";
            market.description = "Central marketplace that attracts merchants and generates trade income.";
            market.category = BuildingCategory.Economy;
            market.baseCost = 500;
            market.baseConstructionTime = 2;
            market.maxLevel = 3;
            market.goldIncomePerLevel = 35;
            market.tradeIncomePerLevel = 15;
            market.attractsBourgeoisPerLevel = 30;
            buildings.Add(market);
            
            // ===================== MILITARY BUILDINGS =====================
            
            // Barracks - Infantry recruitment
            var barracks = ScriptableObject.CreateInstance<BuildingData>();
            barracks.buildingId = "barracks";
            barracks.displayName = "Barracks";
            barracks.description = "Military quarters that enable recruitment of infantry regiments.";
            barracks.category = BuildingCategory.Military;
            barracks.baseCost = 800;
            barracks.baseConstructionTime = 2;
            barracks.maxLevel = 4;
            barracks.recruitmentSlotsPerLevel = 1;
            barracks.garrisonBonusPerLevel = 100;
            barracks.enablesUnitType = true;
            barracks.unlockedUnitType = UnitType.LineInfantry;
            buildings.Add(barracks);
            
            // Stables - Cavalry recruitment
            var stables = ScriptableObject.CreateInstance<BuildingData>();
            stables.buildingId = "stables";
            stables.displayName = "Cavalry Stables";
            stables.description = "Breeds and trains horses for cavalry regiments.";
            stables.category = BuildingCategory.Military;
            stables.baseCost = 1200;
            stables.baseConstructionTime = 3;
            stables.maxLevel = 3;
            stables.requiredCityLevel = 2;
            stables.recruitmentSlotsPerLevel = 1;
            stables.garrisonBonusPerLevel = 50;
            stables.enablesUnitType = true;
            stables.unlockedUnitType = UnitType.Cavalry;
            buildings.Add(stables);
            
            // Armory - Artillery recruitment
            var armory = ScriptableObject.CreateInstance<BuildingData>();
            armory.buildingId = "armory";
            armory.displayName = "Ordnance Armory";
            armory.description = "Foundry and workshop for casting cannons and producing ammunition.";
            armory.category = BuildingCategory.Military;
            armory.baseCost = 1500;
            armory.baseConstructionTime = 3;
            armory.maxLevel = 3;
            armory.requiredCityLevel = 2;
            armory.prerequisiteBuildings = new List<string> { "barracks" };
            armory.recruitmentSlotsPerLevel = 1;
            armory.enablesUnitType = true;
            armory.unlockedUnitType = UnitType.Artillery;
            buildings.Add(armory);
            
            // Fortress - Defense
            var fortress = ScriptableObject.CreateInstance<BuildingData>();
            fortress.buildingId = "fortress";
            fortress.displayName = "Fortress";
            fortress.description = "Mighty fortifications that protect the city and boost garrison capacity.";
            fortress.category = BuildingCategory.Military;
            fortress.baseCost = 2000;
            fortress.baseConstructionTime = 4;
            fortress.maxLevel = 4;
            fortress.requiredCityLevel = 3;
            fortress.defenseBonusPerLevel = 5;
            fortress.garrisonBonusPerLevel = 250;
            fortress.publicOrderPerLevel = 3;
            buildings.Add(fortress);
            
            // Military Academy - Veteran units
            var academy = ScriptableObject.CreateInstance<BuildingData>();
            academy.buildingId = "military_academy";
            academy.displayName = "Military Academy";
            academy.description = "Trains officers and improves the quality of recruited troops.";
            academy.category = BuildingCategory.Military;
            academy.baseCost = 3000;
            academy.baseConstructionTime = 5;
            academy.maxLevel = 2;
            academy.requiredCityLevel = 4;
            academy.prerequisiteBuildings = new List<string> { "barracks", "armory" };
            academy.recruitmentSpeedBonus = 0.2f;
            buildings.Add(academy);
            
            // ===================== INFRASTRUCTURE =====================
            
            // Roads - Trade and movement
            var roads = ScriptableObject.CreateInstance<BuildingData>();
            roads.buildingId = "roads";
            roads.displayName = "Improved Roads";
            roads.description = "Better roads facilitate trade and army movement.";
            roads.category = BuildingCategory.Infrastructure;
            roads.baseCost = 300;
            roads.baseConstructionTime = 2;
            roads.maxLevel = 3;
            roads.goldIncomePerLevel = 15;
            roads.tradeIncomePerLevel = 10;
            buildings.Add(roads);
            
            // Granary - Food storage
            var granary = ScriptableObject.CreateInstance<BuildingData>();
            granary.buildingId = "granary";
            granary.displayName = "Granary";
            granary.description = "Stores food supplies, reducing famine risk and enabling population growth.";
            granary.category = BuildingCategory.Infrastructure;
            granary.baseCost = 400;
            granary.baseConstructionTime = 2;
            granary.maxLevel = 3;
            granary.foodProductionPerLevel = 15;
            granary.populationGrowthPerLevel = 3;
            buildings.Add(granary);
            
            // Aqueduct - Population growth
            var aqueduct = ScriptableObject.CreateInstance<BuildingData>();
            aqueduct.buildingId = "aqueduct";
            aqueduct.displayName = "Aqueduct";
            aqueduct.description = "Brings fresh water to the city, greatly increasing population growth.";
            aqueduct.category = BuildingCategory.Infrastructure;
            aqueduct.baseCost = 1200;
            aqueduct.baseConstructionTime = 3;
            aqueduct.maxLevel = 2;
            aqueduct.requiredCityLevel = 2;
            aqueduct.populationGrowthPerLevel = 5;
            aqueduct.publicOrderPerLevel = 3;
            buildings.Add(aqueduct);
            
            // Customs House - Trade income
            var customs = ScriptableObject.CreateInstance<BuildingData>();
            customs.buildingId = "customs_house";
            customs.displayName = "Customs House";
            customs.description = "Collects tariffs on goods passing through the province.";
            customs.category = BuildingCategory.Infrastructure;
            customs.baseCost = 800;
            customs.baseConstructionTime = 2;
            customs.maxLevel = 3;
            customs.requiredCityLevel = 2;
            customs.goldIncomePerLevel = 30;
            customs.tradeIncomePerLevel = 25;
            buildings.Add(customs);
            
            // Port - Coastal cities only
            var port = ScriptableObject.CreateInstance<BuildingData>();
            port.buildingId = "port";
            port.displayName = "Trading Port";
            port.description = "Enables sea trade and construction of naval units.";
            port.category = BuildingCategory.Coastal;
            port.baseCost = 1000;
            port.baseConstructionTime = 3;
            port.maxLevel = 3;
            port.requiresCoastal = true;
            port.goldIncomePerLevel = 50;
            port.tradeIncomePerLevel = 30;
            buildings.Add(port);
            
            // Shipyard - Naval units
            var shipyard = ScriptableObject.CreateInstance<BuildingData>();
            shipyard.buildingId = "shipyard";
            shipyard.displayName = "Naval Shipyard";
            shipyard.description = "Constructs warships and transport vessels.";
            shipyard.category = BuildingCategory.Coastal;
            shipyard.baseCost = 1800;
            shipyard.baseConstructionTime = 4;
            shipyard.maxLevel = 3;
            shipyard.requiresCoastal = true;
            shipyard.prerequisiteBuildings = new List<string> { "port" };
            shipyard.goldIncomePerLevel = 20;
            buildings.Add(shipyard);
            
            // ===================== RELIGIOUS =====================
            
            // Church - Public order
            var church = ScriptableObject.CreateInstance<BuildingData>();
            church.buildingId = "church";
            church.displayName = "Church";
            church.description = "Provides spiritual guidance and improves public order. Attracts nobles.";
            church.category = BuildingCategory.Religion;
            church.baseCost = 600;
            church.baseConstructionTime = 2;
            church.maxLevel = 3;
            church.publicOrderPerLevel = 8;
            church.attractsNoblesPerLevel = 25;
            buildings.Add(church);
            
            // Cathedral - Major religious center
            var cathedral = ScriptableObject.CreateInstance<BuildingData>();
            cathedral.buildingId = "cathedral";
            cathedral.displayName = "Cathedral";
            cathedral.description = "Magnificent cathedral that greatly improves public order and attracts nobility.";
            cathedral.category = BuildingCategory.Religion;
            cathedral.baseCost = 2500;
            cathedral.baseConstructionTime = 5;
            cathedral.maxLevel = 2;
            cathedral.requiredCityLevel = 4;
            cathedral.prerequisiteBuildings = new List<string> { "church" };
            cathedral.publicOrderPerLevel = 15;
            cathedral.attractsNoblesPerLevel = 75;
            cathedral.goldIncomePerLevel = 20;
            buildings.Add(cathedral);
            
            // ===================== ACADEMIC =====================
            
            // University - Research
            var university = ScriptableObject.CreateInstance<BuildingData>();
            university.buildingId = "university";
            university.displayName = "University";
            university.description = "Center of learning that accelerates research and attracts educated classes.";
            university.category = BuildingCategory.Academic;
            university.baseCost = 1500;
            university.baseConstructionTime = 4;
            university.maxLevel = 3;
            university.requiredCityLevel = 3;
            university.researchSpeedBonus = 0.15f;
            university.attractsBourgeoisPerLevel = 40;
            university.attractsNoblesPerLevel = 20;
            buildings.Add(university);
            
            // Library - Additional research
            var library = ScriptableObject.CreateInstance<BuildingData>();
            library.buildingId = "library";
            library.displayName = "Royal Library";
            library.description = "Repository of knowledge that supports research efforts.";
            library.category = BuildingCategory.Academic;
            library.baseCost = 800;
            library.baseConstructionTime = 2;
            library.maxLevel = 2;
            library.requiredCityLevel = 2;
            library.researchSpeedBonus = 0.08f;
            buildings.Add(library);
            
            // ===================== LOAD ICONS FOR ALL =====================
            foreach (var b in buildings)
            {
                b.icon = LoadBuildingIcon(b.buildingId);
            }
            
            return buildings;
        }
        
        public static List<ResearchNode> CreateDefaultResearchTree()
        {
            var researchNodes = new List<ResearchNode>();
            
            // ===================== MILITARY RESEARCH =====================
            
            // Bayonet Drill
            var bayonetDrill = ScriptableObject.CreateInstance<ResearchNode>();
            bayonetDrill.nodeId = "bayonet_drill";
            bayonetDrill.displayName = "Bayonet Drill";
            bayonetDrill.description = "Standardized bayonet combat techniques improve infantry effectiveness.";
            bayonetDrill.category = ResearchCategory.Military;
            bayonetDrill.researchCost = 800;
            bayonetDrill.researchTime = 3;
            researchNodes.Add(bayonetDrill);
            
            // Flintlock Muskets
            var flintlock = ScriptableObject.CreateInstance<ResearchNode>();
            flintlock.nodeId = "flintlock_muskets";
            flintlock.displayName = "Flintlock Mechanism";
            flintlock.description = "Reliable ignition system for muskets. Unlocks advanced infantry units.";
            flintlock.category = ResearchCategory.Military;
            flintlock.researchCost = 1200;
            flintlock.researchTime = 4;
            researchNodes.Add(flintlock);
            
            // Field Artillery
            var fieldArtillery = ScriptableObject.CreateInstance<ResearchNode>();
            fieldArtillery.nodeId = "field_artillery";
            fieldArtillery.displayName = "Mobile Artillery";
            fieldArtillery.description = "Lighter cannons that can keep pace with armies on campaign.";
            fieldArtillery.category = ResearchCategory.Military;
            fieldArtillery.researchCost = 1500;
            fieldArtillery.researchTime = 5;
            fieldArtillery.prerequisiteNodes = new List<string> { "flintlock_muskets" };
            researchNodes.Add(fieldArtillery);
            
            // ===================== ECONOMIC RESEARCH =====================
            
            // Banking
            var banking = ScriptableObject.CreateInstance<ResearchNode>();
            banking.nodeId = "banking";
            banking.displayName = "Banking System";
            banking.description = "Financial institutions that improve economic efficiency.";
            banking.category = ResearchCategory.Economic;
            banking.researchCost = 1000;
            banking.researchTime = 4;
            banking.globalIncomeBonus = 0.1f;
            researchNodes.Add(banking);
            
            // Division of Labor
            var divisionOfLabor = ScriptableObject.CreateInstance<ResearchNode>();
            divisionOfLabor.nodeId = "division_of_labor";
            divisionOfLabor.displayName = "Division of Labor";
            divisionOfLabor.description = "Organized production methods increase manufacturing output.";
            divisionOfLabor.category = ResearchCategory.Economic;
            divisionOfLabor.researchCost = 800;
            divisionOfLabor.researchTime = 3;
            researchNodes.Add(divisionOfLabor);
            
            // ===================== INDUSTRIAL RESEARCH =====================
            
            // Steam Power
            var steamPower = ScriptableObject.CreateInstance<ResearchNode>();
            steamPower.nodeId = "steam_power";
            steamPower.displayName = "Steam Engine";
            steamPower.description = "Harness steam power for industrial applications.";
            steamPower.category = ResearchCategory.Industrial;
            steamPower.researchCost = 2000;
            steamPower.researchTime = 6;
            steamPower.requiredEra = 2;
            steamPower.prerequisiteNodes = new List<string> { "division_of_labor" };
            researchNodes.Add(steamPower);
            
            // ===================== PHILOSOPHICAL =====================
            
            // Enlightenment
            var enlightenment = ScriptableObject.CreateInstance<ResearchNode>();
            enlightenment.nodeId = "enlightenment";
            enlightenment.displayName = "Enlightenment Thought";
            enlightenment.description = "New ideas about governance and society spread through educated circles.";
            enlightenment.category = ResearchCategory.Philosophical;
            enlightenment.researchCost = 600;
            enlightenment.researchTime = 3;
            enlightenment.globalResearchBonus = 0.1f;
            researchNodes.Add(enlightenment);
            
            return researchNodes;
        }
    }
}
