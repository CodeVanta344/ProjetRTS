using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Technology tree system for the campaign.
    /// Each faction has its own TechTree instance tracking researched technologies.
    /// Technologies provide bonuses to units, economy, and unlock new capabilities.
    /// </summary>

    public enum TechCategory
    {
        Military,
        Economy,
        Diplomacy
    }

    [System.Serializable]
    public class Technology
    {
        public string id;
        public string name;
        public string description;
        public TechCategory category;
        public int researchCost;        // Gold cost
        public int turnsToResearch;
        public string[] prerequisites;  // Tech IDs required before this can be researched

        // Bonuses
        public float accuracyBonus;
        public float moraleBonus;
        public float speedBonus;
        public float damageBonus;
        public float goldIncomeBonus;   // Percentage
        public float foodBonus;
        public float ironBonus;
        public float recruitCostReduction; // Percentage
        public float maintenanceReduction; // Percentage
        public float diplomacyBonus;
        public bool unlocksUnitType;
        public UnitType unlockedUnit;
        
        // Building unlocks
        public bool unlocksBuilding;
        public BuildingType unlockedBuilding;
        
        // Population requirement for this technology (for advanced buildings)
        public int requiredPopulation;

        public Technology(string id, string name, string desc, TechCategory cat, int cost, int turns)
        {
            this.id = id;
            this.name = name;
            this.description = desc;
            this.category = cat;
            this.researchCost = cost;
            this.turnsToResearch = turns;
            this.prerequisites = new string[0];
        }
    }

    [System.Serializable]
    public class TechResearchState
    {
        public string techId;
        public int turnsRemaining;
        public bool completed;
    }

    public class TechTree
    {
        private Dictionary<string, Technology> allTechs = new Dictionary<string, Technology>();
        private Dictionary<string, TechResearchState> researchStates = new Dictionary<string, TechResearchState>();
        private string currentResearchId;

        // Cached bonuses (recalculated when tech completes)
        public float TotalAccuracyBonus { get; private set; }
        public float TotalMoraleBonus { get; private set; }
        public float TotalSpeedBonus { get; private set; }
        public float TotalDamageBonus { get; private set; }
        public float TotalGoldIncomeBonus { get; private set; }
        public float TotalFoodBonus { get; private set; }
        public float TotalIronBonus { get; private set; }
        public float TotalRecruitCostReduction { get; private set; }
        public float TotalMaintenanceReduction { get; private set; }
        public float TotalDiplomacyBonus { get; private set; }
        public string CurrentResearchId => currentResearchId;
        public int CurrentResearchTurnsLeft => currentResearchId != null && researchStates.ContainsKey(currentResearchId)
            ? researchStates[currentResearchId].turnsRemaining : 0;

        public TechTree()
        {
            InitializeTechTree();
        }

        private void InitializeTechTree()
        {
            // ============================================================================
            // TIER 1 - BASIC TECHNOLOGIES (Starting Era - Early 1800s)
            // ============================================================================
            
            // === INFANTRY PROGRESSION I ===
            var basicDrill = new Technology("basic_drill", "Basic Drill",
                "Elementary marching formations. Slight morale improvement.",
                TechCategory.Military, 200, 2)
            { moraleBonus = 5f };
            Register(basicDrill);

            var flintlockMechanism = new Technology("flintlock_mechanism", "Flintlock Mechanism",
                "Reliable ignition system. +5% infantry damage.",
                TechCategory.Military, 250, 2)
            { damageBonus = 0.05f };
            Register(flintlockMechanism);

            var bayonetAttachment = new Technology("bayonet_attachment", "Socket Bayonet",
                "Bayonet fits around barrel, allowing firing with bayonet attached. +10% melee damage.",
                TechCategory.Military, 300, 3)
            { damageBonus = 0.10f, prerequisites = new[] { "flintlock_mechanism" } };
            Register(bayonetAttachment);

            // === ARTILLERY PROGRESSION I - Small School ===
            var smallArtillerySchool = new Technology("small_artillery_school", "Small Artillery School",
                "A modest school for training gun crews. +5% artillery damage. Unlocks basic artillery improvements.",
                TechCategory.Military, 350, 3)
            { damageBonus = 0.05f, unlocksBuilding = true, unlockedBuilding = BuildingType.SmallArtillerySchool };
            Register(smallArtillerySchool);

            var bronzeCasting = new Technology("bronze_casting", "Bronze Cannon Casting",
                "Reliable but heavy artillery pieces. +10% artillery damage.",
                TechCategory.Military, 400, 3)
            { damageBonus = 0.10f, prerequisites = new[] { "small_artillery_school" } };
            Register(bronzeCasting);

            var gunpowderRefinement = new Technology("gunpowder_refinement", "Refined Gunpowder",
                "Better quality powder for more consistent shots. +10% artillery accuracy.",
                TechCategory.Military, 450, 3)
            { accuracyBonus = 0.10f, prerequisites = new[] { "small_artillery_school" } };
            Register(gunpowderRefinement);

            // === CAVALRY PROGRESSION I ===
            var basicRiding = new Technology("basic_riding", "Basic Horsemanship",
                "Standard cavalry training. +5% cavalry speed.",
                TechCategory.Military, 250, 2)
            { speedBonus = 0.05f };
            Register(basicRiding);

            var carbineTraining = new Technology("carbine_training", "Carbine Training",
                "Firearms for cavalry. +5% cavalry damage.",
                TechCategory.Military, 300, 2)
            { damageBonus = 0.05f, prerequisites = new[] { "basic_riding" } };
            Register(carbineTraining);

            // === ECONOMIC FOUNDATION I ===
            var cropRotation = new Technology("crop_rotation", "Three-Field Rotation",
                "Sustainable farming methods. +15% food production.",
                TechCategory.Economy, 200, 2)
            { foodBonus = 0.15f };
            Register(cropRotation);

            var basicMining = new Technology("basic_mining", "Basic Mining",
                "Simple extraction techniques. +10% iron production.",
                TechCategory.Economy, 250, 2)
            { ironBonus = 0.10f };
            Register(basicMining);

            var localMarkets = new Technology("local_markets", "Local Market Charters",
                "Trade rights for towns. +10% gold income.",
                TechCategory.Economy, 200, 2)
            { goldIncomeBonus = 0.10f };
            Register(localMarkets);

            var villageBarracks = new Technology("village_barracks", "Village Barracks",
                "Basic training grounds. Unlocks small-scale recruitment.",
                TechCategory.Economy, 300, 2)
            { unlocksBuilding = true, unlockedBuilding = BuildingType.VillageBarracks };
            Register(villageBarracks);

            // === DIPLOMATIC FOUNDATION ===
            var couriers = new Technology("couriers", "Courier Network",
                "Fast message delivery between provinces. +10 diplomacy range.",
                TechCategory.Diplomacy, 150, 2)
            { diplomacyBonus = 10f };
            Register(couriers);

            // ============================================================================
            // TIER 2 - EARLY MODERN ERA (Napoleonic Early Wars)
            // ============================================================================

            // === INFANTRY PROGRESSION II ===
            var volleyFire = new Technology("volley_fire", "Volley Fire Doctrine",
                "Coordinated mass firing. +15% accuracy for infantry.",
                TechCategory.Military, 400, 3)
            { accuracyBonus = 0.15f, prerequisites = new[] { "basic_drill" } };
            Register(volleyFire);

            var platoonSystem = new Technology("platoon_system", "Platoon System",
                "Flexible unit organization. +10% infantry damage.",
                TechCategory.Military, 450, 3)
            { damageBonus = 0.10f, prerequisites = new[] { "volley_fire" } };
            Register(platoonSystem);

            var skirmishTactics = new Technology("skirmish_tactics", "Skirmisher Training",
                "Light infantry screen tactics. Unlocks Light Infantry.",
                TechCategory.Military, 400, 3)
            { unlocksUnitType = true, unlockedUnit = UnitType.LightInfantry,
              prerequisites = new[] { "bayonet_attachment" } };
            Register(skirmishTactics);

            // === ARTILLERY PROGRESSION II - Provincial School ===
            var provincialArtillerySchool = new Technology("provincial_artillery_school", "Provincial Artillery School",
                "Larger training facility for gunners. +10% artillery damage. Requires population 5000.",
                TechCategory.Military, 600, 4)
            { damageBonus = 0.10f, unlocksBuilding = true, unlockedBuilding = BuildingType.ProvincialArtillerySchool,
              prerequisites = new[] { "small_artillery_school", "bronze_casting" },
              requiredPopulation = 5000 };
            Register(provincialArtillerySchool);

            var ironBarrel = new Technology("iron_barrel", "Iron Barrel Casting",
                "Stronger, lighter cannons. +15% artillery damage.",
                TechCategory.Military, 500, 3)
            { damageBonus = 0.15f, prerequisites = new[] { "bronze_casting" } };
            Register(ironBarrel);

            var artillerySights = new Technology("artillery_sights", "Artillery Sights",
                "Improved aiming devices. +15% artillery accuracy.",
                TechCategory.Military, 450, 3)
            { accuracyBonus = 0.15f, prerequisites = new[] { "gunpowder_refinement" } };
            Register(artillerySights);

            var canisterShot = new Technology("canister_shot", "Canister Shot",
                "Anti-personnel ammunition. +20% damage vs infantry.",
                TechCategory.Military, 550, 3)
            { damageBonus = 0.20f, prerequisites = new[] { "provincial_artillery_school" } };
            Register(canisterShot);

            // === CAVALRY PROGRESSION II ===
            var shockTactics = new Technology("shock_tactics", "Cavalry Shock Tactics",
                "Devastating charges. +15% cavalry damage.",
                TechCategory.Military, 450, 3)
            { damageBonus = 0.15f, prerequisites = new[] { "carbine_training" } };
            Register(shockTactics);

            var hussarTradition = new Technology("hussar_tradition", "Hussar Traditions",
                "Elite light cavalry recruitment. Unlocks Hussars.",
                TechCategory.Military, 500, 4)
            { unlocksUnitType = true, unlockedUnit = UnitType.Hussar,
              prerequisites = new[] { "shock_tactics", "skirmish_tactics" } };
            Register(hussarTradition);

            var dragoonTraining = new Technology("dragoon_training", "Dragoon Training",
                "Mounted infantry tactics. +10% cavalry flexibility.",
                TechCategory.Military, 400, 3)
            { speedBonus = 0.05f, damageBonus = 0.05f, prerequisites = new[] { "carbine_training" } };
            Register(dragoonTraining);

            // === ECONOMIC PROGRESSION II ===
            var seedSelection = new Technology("seed_selection", "Selective Breeding",
                "Better crop yields through genetics. +20% food production.",
                TechCategory.Economy, 400, 3)
            { foodBonus = 0.20f, prerequisites = new[] { "crop_rotation" } };
            Register(seedSelection);

            var deepMining = new Technology("deep_mining", "Deep Shaft Mining",
                "Access richer ore deposits. +20% iron production.",
                TechCategory.Economy, 450, 3)
            { ironBonus = 0.20f, prerequisites = new[] { "basic_mining" } };
            Register(deepMining);

            var charteredCompanies = new Technology("chartered_companies", "Chartered Companies",
                "State-backed trade enterprises. +20% gold income.",
                TechCategory.Economy, 500, 3)
            { goldIncomeBonus = 0.20f, prerequisites = new[] { "local_markets" } };
            Register(charteredCompanies);

            var provincialBarracks = new Technology("provincial_barracks", "Provincial Barracks",
                "Major recruitment center. Requires population 8000.",
                TechCategory.Economy, 600, 3)
            { unlocksBuilding = true, unlockedBuilding = BuildingType.ProvincialBarracks,
              recruitCostReduction = 0.10f, prerequisites = new[] { "village_barracks" },
              requiredPopulation = 8000 };
            Register(provincialBarracks);

            // === DIPLOMATIC PROGRESSION ===
            var ambassadors = new Technology("ambassadors", "Permanent Ambassadors",
                "Professional diplomatic corps. +20 relations.",
                TechCategory.Diplomacy, 400, 3)
            { diplomacyBonus = 20f, prerequisites = new[] { "couriers" } };
            Register(ambassadors);

            var treaties = new Technology("treaties", "Formal Treaties",
                "Binding diplomatic agreements. +15% alliance chance.",
                TechCategory.Diplomacy, 450, 3)
            { diplomacyBonus = 15f, prerequisites = new[] { "ambassadors" } };
            Register(treaties);

            // ============================================================================
            // TIER 3 - NAPOLEONIC GOLDEN AGE
            // ============================================================================

            // === INFANTRY PROGRESSION III ===
            var bayonetCharge = new Technology("bayonet_charge", "Bayonet Charge Doctrine",
                "Aggressive infantry tactics. +20% melee damage.",
                TechCategory.Military, 600, 4)
            { damageBonus = 0.20f, prerequisites = new[] { "platoon_system" } };
            Register(bayonetCharge);

            var grenadierTraining = new Technology("grenadier_training", "Grenadier Selection",
                "Elite heavy infantry. Unlocks Grenadiers. Requires tall soldiers.",
                TechCategory.Military, 700, 4)
            { unlocksUnitType = true, unlockedUnit = UnitType.Grenadier,
              moraleBonus = 10f, prerequisites = new[] { "bayonet_charge" } };
            Register(grenadierTraining);

            var firingByRank = new Technology("firing_by_rank", "Firing by Rank",
                "Continuous rolling volleys. +20% rate of fire.",
                TechCategory.Military, 650, 4)
            { damageBonus = 0.20f, prerequisites = new[] { "platoon_system", "volley_fire" } };
            Register(firingByRank);

            var lightInfantryTactics = new Technology("light_infantry_tactics", "Advanced Light Infantry",
                "Superior skirmishing. +15% light infantry damage.",
                TechCategory.Military, 550, 3)
            { damageBonus = 0.15f, prerequisites = new[] { "skirmish_tactics" } };
            Register(lightInfantryTactics);

            // === ARTILLERY PROGRESSION III - Royal Academy ===
            var royalArtilleryAcademy = new Technology("royal_artillery_academy", "Royal Artillery Academy",
                "Premier institution for artillery science. +15% artillery damage. Requires population 15000.",
                TechCategory.Military, 1000, 5)
            { damageBonus = 0.15f, accuracyBonus = 0.10f, unlocksBuilding = true, 
              unlockedBuilding = BuildingType.RoyalArtilleryAcademy,
              prerequisites = new[] { "provincial_artillery_school", "iron_barrel", "artillery_sights" },
              requiredPopulation = 15000 };
            Register(royalArtilleryAcademy);

            var gribeauvalSystem = new Technology("gribeauval_system", "Gribeauval System",
                "Standardized artillery calibers. +20% artillery effectiveness.",
                TechCategory.Military, 800, 4)
            { damageBonus = 0.20f, accuracyBonus = 0.10f, 
              prerequisites = new[] { "royal_artillery_academy" } };
            Register(gribeauvalSystem);

            var horseArtillery = new Technology("horse_artillery", "Horse Artillery",
                "Mobile artillery support. +25% artillery speed.",
                TechCategory.Military, 750, 4)
            { speedBonus = 0.25f, prerequisites = new[] { "royal_artillery_academy" } };
            Register(horseArtillery);

            var shrapnelShell = new Technology("shrapnel_shell", "Shrapnel Shell",
                "Exploding anti-personnel rounds. +25% damage vs infantry.",
                TechCategory.Military, 850, 4)
            { damageBonus = 0.25f, prerequisites = new[] { "canister_shot" } };
            Register(shrapnelShell);

            var elevationSights = new Technology("elevation_sights", "Elevation Quadrants",
                "Precise range calculation. +20% artillery accuracy.",
                TechCategory.Military, 700, 4)
            { accuracyBonus = 0.20f, prerequisites = new[] { "artillery_sights" } };
            Register(elevationSights);

            // === CAVALRY PROGRESSION III ===
            var lancerTradition = new Technology("lancer_tradition", "Lancer Traditions",
                "Shock cavalry with reach advantage. Unlocks Lancers.",
                TechCategory.Military, 700, 4)
            { unlocksUnitType = true, unlockedUnit = UnitType.Lancer,
              damageBonus = 0.15f, prerequisites = new[] { "hussar_tradition", "shock_tactics" } };
            Register(lancerTradition);

            var heavyCavalryArmor = new Technology("heavy_cavalry_armor", "Cuirassier Armor",
                "Breastplates for heavy cavalry. +20% cavalry defense.",
                TechCategory.Military, 650, 4)
            { moraleBonus = 15f, prerequisites = new[] { "shock_tactics" } };
            Register(heavyCavalryArmor);

            var cavalryCarbine = new Technology("cavalry_carbine", "Improved Cavalry Carbines",
                "Better firearms for mounted troops. +15% cavalry damage.",
                TechCategory.Military, 600, 4)
            { damageBonus = 0.15f, accuracyBonus = 0.10f, 
              prerequisites = new[] { "dragoon_training" } };
            Register(cavalryCarbine);

            // === ECONOMIC PROGRESSION III ===
            var agriculturalRevolution = new Technology("agricultural_revolution", "Agricultural Revolution",
                "Scientific farming methods. +25% food production.",
                TechCategory.Economy, 700, 4)
            { foodBonus = 0.25f, prerequisites = new[] { "seed_selection" } };
            Register(agriculturalRevolution);

            var blastFurnace = new Technology("blast_furnace", "Blast Furnace",
                "Industrial iron production. +25% iron production.",
                TechCategory.Economy, 800, 4)
            { ironBonus = 0.25f, prerequisites = new[] { "deep_mining" } };
            Register(blastFurnace);

            var nationalBank = new Technology("national_bank", "National Bank",
                "Centralized financial system. +25% gold income.",
                TechCategory.Economy, 750, 4)
            { goldIncomeBonus = 0.25f, prerequisites = new[] { "chartered_companies" } };
            Register(nationalBank);

            var militaryAcademy = new Technology("military_academy", "Military Academy",
                "Officer training institution. +10% all unit stats. Requires population 12000.",
                TechCategory.Economy, 900, 5)
            { unlocksBuilding = true, unlockedBuilding = BuildingType.MilitaryAcademy,
              accuracyBonus = 0.10f, damageBonus = 0.10f, moraleBonus = 10f,
              prerequisites = new[] { "provincial_barracks", "platoon_system" },
              requiredPopulation = 12000 };
            Register(militaryAcademy);

            var conscriptionLaw = new Technology("conscription_law", "Conscription Law",
                "Nation-wide military service. -25% recruit costs.",
                TechCategory.Economy, 800, 4)
            { recruitCostReduction = 0.25f, prerequisites = new[] { "provincial_barracks" } };
            Register(conscriptionLaw);

            // === DIPLOMATIC PROGRESSION ===
            var congressSystem = new Technology("congress_system", "Congress System",
                "Great power diplomacy. +30 relations, alliance improvements.",
                TechCategory.Diplomacy, 600, 4)
            { diplomacyBonus = 30f, prerequisites = new[] { "treaties" } };
            Register(congressSystem);

            var espionageNetwork = new Technology("espionage_network", "Espionage Network",
                "Intelligence operations. See enemy movements.",
                TechCategory.Diplomacy, 700, 4)
            { prerequisites = new[] { "ambassadors" } };
            Register(espionageNetwork);

            // ============================================================================
            // TIER 4 - LATE NAPOLEONIC / EARLY INDUSTRIAL
            // ============================================================================

            // === INFANTRY PROGRESSION IV ===
            var combinedArms = new Technology("combined_arms", "Combined Arms Tactics",
                "Infantry, cavalry, artillery coordination. +10% all combat stats.",
                TechCategory.Military, 900, 5)
            { accuracyBonus = 0.10f, damageBonus = 0.10f, speedBonus = 0.10f, moraleBonus = 10f,
              prerequisites = new[] { "firing_by_rank", "light_infantry_tactics" } };
            Register(combinedArms);

            var disciplinedFire = new Technology("disciplined_fire", "Disciplined Volleys",
                "Professional infantry fire. +25% accuracy.",
                TechCategory.Military, 850, 5)
            { accuracyBonus = 0.25f, prerequisites = new[] { "firing_by_rank" } };
            Register(disciplinedFire);

            var defensivePositions = new Technology("defensive_positions", "Field Fortification",
                "Rapid defensive works. +20% defense bonus.",
                TechCategory.Military, 800, 5)
            { moraleBonus = 20f, prerequisites = new[] { "combined_arms" } };
            Register(defensivePositions);

            // === ARTILLERY PROGRESSION IV - Grand Academy ===
            var grandArtilleryAcademy = new Technology("grand_artillery_academy", "Grand Artillery Academy",
                "World-class artillery institution. +20% artillery damage. Requires population 25000.",
                TechCategory.Military, 1400, 6)
            { damageBonus = 0.20f, accuracyBonus = 0.15f, unlocksBuilding = true,
              unlockedBuilding = BuildingType.GrandArtilleryAcademy,
              prerequisites = new[] { "royal_artillery_academy", "gribeauval_system" },
              requiredPopulation = 25000 };
            Register(grandArtilleryAcademy);

            var percussionIgnition = new Technology("percussion_ignition", "Percussion Ignition",
                "Reliable all-weather firing. +25% artillery reliability.",
                TechCategory.Military, 1000, 5)
            { accuracyBonus = 0.25f, prerequisites = new[] { "grand_artillery_academy" } };
            Register(percussionIgnition);

            var rifledCannons = new Technology("rifled_cannons", "Rifled Artillery",
                "Spin-stabilized projectiles. +30% artillery accuracy.",
                TechCategory.Military, 1200, 6)
            { accuracyBonus = 0.30f, damageBonus = 0.10f,
              prerequisites = new[] { "grand_artillery_academy", "elevation_sights" } };
            Register(rifledCannons);

            var rocketArtillery = new Technology("rocket_artillery", "Rocket Artillery",
                "Congreve rockets for area bombardment. Special weapon.",
                TechCategory.Military, 1100, 5)
            { damageBonus = 0.20f, prerequisites = new[] { "grand_artillery_academy" } };
            Register(rocketArtillery);

            var heavySiegeGuns = new Technology("heavy_siege_guns", "Heavy Siege Artillery",
                "Massive fortress breakers. +35% damage vs fortifications.",
                TechCategory.Military, 1000, 5)
            { damageBonus = 0.35f, prerequisites = new[] { "gribeauval_system" } };
            Register(heavySiegeGuns);

            // === CAVALRY PROGRESSION IV ===
            var cavalryDiscipline = new Technology("cavalry_discipline", "Cavalry Discipline",
                "Professional cavalry standards. +20% cavalry stats.",
                TechCategory.Military, 900, 5)
            { damageBonus = 0.20f, moraleBonus = 15f, speedBonus = 0.10f,
              prerequisites = new[] { "heavy_cavalry_armor", "cavalry_carbine" } };
            Register(cavalryDiscipline);

            var mountedRifles = new Technology("mounted_rifles", "Mounted Rifles",
                "Accurate fire from horseback. +25% cavalry accuracy.",
                TechCategory.Military, 850, 5)
            { accuracyBonus = 0.25f, prerequisites = new[] { "cavalry_carbine" } };
            Register(mountedRifles);

            var cuirassierCharge = new Technology("cuirassier_charge", "Cuirassier Charge",
                "Devastating heavy cavalry assaults. +25% cavalry charge damage.",
                TechCategory.Military, 950, 5)
            { damageBonus = 0.25f, moraleBonus = 10f,
              prerequisites = new[] { "cavalry_discipline", "heavy_cavalry_armor" } };
            Register(cuirassierCharge);

            // === ECONOMIC PROGRESSION IV ===
            var mechanizedFarming = new Technology("mechanized_farming", "Mechanized Farming",
                "Early agricultural machines. +30% food production.",
                TechCategory.Economy, 900, 5)
            { foodBonus = 0.30f, prerequisites = new[] { "agricultural_revolution" } };
            Register(mechanizedFarming);

            var steamPumping = new Technology("steam_pumping", "Steam Mine Pumps",
                "Remove water from deep mines. +30% iron production.",
                TechCategory.Economy, 1000, 5)
            { ironBonus = 0.30f, prerequisites = new[] { "blast_furnace" } };
            Register(steamPumping);

            var stockExchange = new Technology("stock_exchange", "Stock Exchange",
                "Capital markets. +30% gold income.",
                TechCategory.Economy, 950, 5)
            { goldIncomeBonus = 0.30f, prerequisites = new[] { "national_bank" } };
            Register(stockExchange);

            var railwayNetwork = new Technology("railway_network", "Railway Network",
                "Steam-powered logistics. -30% maintenance, +20% movement.",
                TechCategory.Economy, 1100, 6)
            { maintenanceReduction = 0.30f, speedBonus = 0.20f,
              prerequisites = new[] { "national_bank", "steam_pumping" } };
            Register(railwayNetwork);

            var royalMilitaryCollege = new Technology("royal_military_college", "Royal Military College",
                "Elite officer training. +15% all unit stats. Requires population 20000.",
                TechCategory.Economy, 1200, 6)
            { unlocksBuilding = true, unlockedBuilding = BuildingType.RoyalMilitaryCollege,
              accuracyBonus = 0.15f, damageBonus = 0.15f, moraleBonus = 15f,
              prerequisites = new[] { "military_academy", "combined_arms" },
              requiredPopulation = 20000 };
            Register(royalMilitaryCollege);

            var totalWarEconomy = new Technology("total_war_economy", "Total War Economy",
                "Nation fully mobilized for war. -30% costs, +15% production.",
                TechCategory.Economy, 1000, 5)
            { recruitCostReduction = 0.30f, goldIncomeBonus = 0.15f,
              maintenanceReduction = 0.15f, prerequisites = new[] { "conscription_law", "national_bank" } };
            Register(totalWarEconomy);

            // === DIPLOMATIC PROGRESSION ===
            var balanceOfPower = new Technology("balance_of_power", "Balance of Power",
                "Maintain equilibrium through diplomacy. +40 relations.",
                TechCategory.Diplomacy, 800, 5)
            { diplomacyBonus = 40f, prerequisites = new[] { "congress_system" } };
            Register(balanceOfPower);

            var codeSystem = new Technology("code_system", "Diplomatic Codes",
                "Secure diplomatic communications. Intelligence advantage.",
                TechCategory.Diplomacy, 700, 4)
            { prerequisites = new[] { "espionage_network" } };
            Register(codeSystem);

            // ============================================================================
            // TIER 5 - INDUSTRIAL REVOLUTION (Late Game)
            // ============================================================================

            // === INFANTRY PROGRESSION V ===
            var miniéRifle = new Technology("minie_rifle", "Minié Rifle",
                "Rifled muskets for all infantry. +30% accuracy.",
                TechCategory.Military, 1200, 6)
            { accuracyBonus = 0.30f, damageBonus = 0.15f,
              prerequisites = new[] { "disciplined_fire" } };
            Register(miniéRifle);

            var rifledMusketry = new Technology("rifled_musketry", "Rifled Musketry",
                "Standard rifled weapons. +35% accuracy.",
                TechCategory.Military, 1300, 6)
            { accuracyBonus = 0.35f, damageBonus = 0.10f,
              prerequisites = new[] { "minie_rifle" } };
            Register(rifledMusketry);

            var extendedOrder = new Technology("extended_order", "Extended Order Tactics",
                "Loose formations against artillery. +25% defense.",
                TechCategory.Military, 1100, 6)
            { moraleBonus = 25f, prerequisites = new[] { "defensive_positions", "minie_rifle" } };
            Register(extendedOrder);

            var breechLoading = new Technology("breech_loading", "Breech-Loading Firearms",
                "Faster reload from behind cover. +30% rate of fire.",
                TechCategory.Military, 1400, 7)
            { damageBonus = 0.30f, prerequisites = new[] { "rifled_musketry" } };
            Register(breechLoading);

            // === ARTILLERY PROGRESSION V - Imperial Academy ===
            var imperialArtilleryAcademy = new Technology("imperial_artillery_academy", "Imperial Artillery Academy",
                "The pinnacle of artillery science. +25% artillery damage. Requires metropolis (30000+ pop).",
                TechCategory.Military, 1800, 8)
            { damageBonus = 0.25f, accuracyBonus = 0.20f, unlocksBuilding = true,
              unlockedBuilding = BuildingType.ImperialArtilleryAcademy,
              prerequisites = new[] { "grand_artillery_academy", "rifled_cannons" },
              requiredPopulation = 30000 };
            Register(imperialArtilleryAcademy);

            var steelBarrels = new Technology("steel_barrels", "Steel Barrels",
                "Strongest, lightest cannons ever. +20% damage, +15% speed.",
                TechCategory.Military, 1500, 7)
            { damageBonus = 0.20f, speedBonus = 0.15f,
              prerequisites = new[] { "imperial_artillery_academy" } };
            Register(steelBarrels);

            var recoilSystems = new Technology("recoil_systems", "Hydro-Pneumatic Recoil",
                "Rapid, accurate fire. +30% artillery rate of fire.",
                TechCategory.Military, 1600, 7)
            { damageBonus = 0.30f, prerequisites = new[] { "imperial_artillery_academy" } };
            Register(recoilSystems);

            var indirectFire = new Technology("indirect_fire", "Indirect Fire Techniques",
                "Fire over obstacles. +25% tactical flexibility.",
                TechCategory.Military, 1400, 7)
            { accuracyBonus = 0.25f, prerequisites = new[] { "rifled_cannons" } };
            Register(indirectFire);

            var explosiveShells = new Technology("explosive_shells", "High-Explosive Shells",
                "Devastating fragmentation. +35% damage.",
                TechCategory.Military, 1500, 7)
            { damageBonus = 0.35f, prerequisites = new[] { "imperial_artillery_academy" } };
            Register(explosiveShells);

            // === CAVALRY PROGRESSION V ===
            var modernCavalry = new Technology("modern_cavalry", "Modern Cavalry",
                "Adapting cavalry to changing warfare. +25% all cavalry stats.",
                TechCategory.Military, 1200, 6)
            { damageBonus = 0.25f, moraleBonus = 20f, speedBonus = 0.15f,
              prerequisites = new[] { "cavalry_discipline", "mounted_rifles" } };
            Register(modernCavalry);

            var cavalrySkirmishers = new Technology("cavalry_skirmishers", "Cavalry Skirmishers",
                "Mounted light troops. +30% cavalry flexibility.",
                TechCategory.Military, 1100, 6)
            { accuracyBonus = 0.30f, speedBonus = 0.15f,
              prerequisites = new[] { "modern_cavalry" } };
            Register(cavalrySkirmishers);

            // === ECONOMIC PROGRESSION V ===
            var industrialAgriculture = new Technology("industrial_agriculture", "Industrial Agriculture",
                "Factory farming techniques. +35% food production.",
                TechCategory.Economy, 1200, 6)
            { foodBonus = 0.35f, prerequisites = new[] { "mechanized_farming" } };
            Register(industrialAgriculture);

            var steelIndustry = new Technology("steel_industry", "Bessemer Steel",
                "Mass-produced steel. +35% iron production.",
                TechCategory.Economy, 1300, 7)
            { ironBonus = 0.35f, prerequisites = new[] { "steam_pumping" } };
            Register(steelIndustry);

            var internationalFinance = new Technology("international_finance", "International Finance",
                "Global capital markets. +35% gold income.",
                TechCategory.Economy, 1250, 6)
            { goldIncomeBonus = 0.35f, prerequisites = new[] { "stock_exchange" } };
            Register(internationalFinance);

            var warIndustrialComplex = new Technology("war_industrial_complex", "War Industrial Complex",
                "State-directed war production. +20% all resources, -35% costs.",
                TechCategory.Economy, 1400, 7)
            { goldIncomeBonus = 0.20f, foodBonus = 0.20f, ironBonus = 0.20f,
              recruitCostReduction = 0.35f, maintenanceReduction = 0.20f,
              prerequisites = new[] { "total_war_economy", "steel_industry" } };
            Register(warIndustrialComplex);

            var militaryUniversity = new Technology("military_university", "Military University",
                "Highest level officer training. +20% all unit stats. Requires population 40000.",
                TechCategory.Economy, 1500, 8)
            { unlocksBuilding = true, unlockedBuilding = BuildingType.MilitaryUniversity,
              accuracyBonus = 0.20f, damageBonus = 0.20f, moraleBonus = 20f,
              prerequisites = new[] { "royal_military_college", "war_industrial_complex" },
              requiredPopulation = 40000 };
            Register(militaryUniversity);

            // === DIPLOMATIC PROGRESSION ===
            var greatPowerPolitics = new Technology("great_power_politics", "Great Power Politics",
                "Dominate the Concert of Europe. +50 relations, vassal bonuses.",
                TechCategory.Diplomacy, 1000, 6)
            { diplomacyBonus = 50f, prerequisites = new[] { "balance_of_power" } };
            Register(greatPowerPolitics);

            var intelligenceAgency = new Technology("intelligence_agency", "Intelligence Agency",
                "Professional spy network. Complete intelligence dominance.",
                TechCategory.Diplomacy, 900, 6)
            { prerequisites = new[] { "code_system", "espionage_network" } };
            Register(intelligenceAgency);

            // ============================================================================
            // UNIQUE & ADVANCED TECHNOLOGIES (Late Game Specializations)
            // ============================================================================

            // Elite Unit Unlocks
            var oldGuard = new Technology("old_guard", "The Old Guard",
                "Napoleon's immortal veterans. Unlocks elite Guard units.",
                TechCategory.Military, 2000, 8)
            { unlocksUnitType = true, unlockedUnit = UnitType.ImperialGuard,
              accuracyBonus = 0.25f, damageBonus = 0.25f, moraleBonus = 30f,
              prerequisites = new[] { "breech_loading", "royal_military_college" } };
            Register(oldGuard);

            var guardCavalry = new Technology("guard_cavalry", "Guard Cavalry",
                "The finest horsemen in Europe. Unlocks elite cavalry.",
                TechCategory.Military, 1800, 7)
            { unlocksUnitType = true, unlockedUnit = UnitType.GuardCavalry,
              damageBonus = 0.20f, moraleBonus = 25f,
              prerequisites = new[] { "cuirassier_charge", "old_guard" } };
            Register(guardCavalry);

            var guardArtillery = new Technology("guard_artillery", "Guard Artillery",
                "The Emperor's personal guns. Unlocks elite artillery.",
                TechCategory.Military, 1700, 7)
            { unlocksUnitType = true, unlockedUnit = UnitType.GuardArtillery,
              damageBonus = 0.30f, accuracyBonus = 0.25f,
              prerequisites = new[] { "recoil_systems", "old_guard" } };
            Register(guardArtillery);

            // Naval Tech (for future expansion)
            var shipOfTheLine = new Technology("ship_of_the_line", "Ship of the Line",
                "Mighty wooden walls. Basic naval power.",
                TechCategory.Military, 1000, 5)
            { prerequisites = new[] { "national_bank" } };
            Register(shipOfTheLine);

            var steamShips = new Technology("steam_ships", "Steam Warships",
                "Ironclad steam vessels. Modern naval power.",
                TechCategory.Military, 1500, 7)
            { prerequisites = new[] { "ship_of_the_line", "steel_industry" } };
            Register(steamShips);

            // Special Fortifications
            var starForts = new Technology("star_forts", "Star Fort Design",
                "Bastion trace italienne. +30% fortification strength.",
                TechCategory.Military, 800, 5)
            { prerequisites = new[] { "defensive_positions" } };
            Register(starForts);

            var polygonalForts = new Technology("polygonal_forts", "Polygonal Forts",
                "Modern fortress design. +40% fortification strength.",
                TechCategory.Military, 1200, 6)
            { prerequisites = new[] { "star_forts", "indirect_fire" } };
            Register(polygonalForts);

            Debug.Log($"[TechTree] Initialized with {allTechs.Count} technologies");
        }

        private void Register(Technology tech)
        {
            allTechs[tech.id] = tech;
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        public Dictionary<string, Technology> GetAllTechs() => allTechs;

        public bool IsResearched(string techId)
        {
            return researchStates.ContainsKey(techId) && researchStates[techId].completed;
        }

        public bool IsResearching(string techId)
        {
            return currentResearchId == techId;
        }

        public bool IsResearching()
        {
            return currentResearchId != null;
        }

        public List<Technology> GetAvailableTechnologies()
        {
            var available = new List<Technology>();
            foreach (var tech in allTechs.Values)
            {
                if (CanResearch(tech.id))
                    available.Add(tech);
            }
            return available;
        }

        public void StartResearch(string techId)
        {
            if (!CanResearch(techId)) return;
            
            Technology tech = allTechs[techId];
            currentResearchId = techId;

            researchStates[techId] = new TechResearchState
            {
                techId = techId,
                turnsRemaining = tech.turnsToResearch,
                completed = false
            };

            Debug.Log($"[TechTree] Started researching: {tech.name}");
        }

        public bool CanResearch(string techId)
        {
            if (!allTechs.ContainsKey(techId)) return false;
            if (IsResearched(techId)) return false;
            if (currentResearchId != null) return false;

            Technology tech = allTechs[techId];
            foreach (string prereq in tech.prerequisites)
            {
                if (!IsResearched(prereq)) return false;
            }
            return true;
        }

        public bool StartResearch(string techId, FactionData faction)
        {
            if (!CanResearch(techId)) return false;

            Technology tech = allTechs[techId];
            if (!faction.CanAfford(tech.researchCost)) return false;

            faction.Spend(tech.researchCost);
            currentResearchId = techId;

            researchStates[techId] = new TechResearchState
            {
                techId = techId,
                turnsRemaining = tech.turnsToResearch,
                completed = false
            };

            Debug.Log($"[TechTree] Started researching: {tech.name} ({tech.turnsToResearch} turns)");
            return true;
        }

        /// <summary>
        /// Call once per turn to advance research.
        /// Returns the completed Technology if research finished this turn, null otherwise.
        /// </summary>
        public Technology AdvanceResearch()
        {
            if (currentResearchId == null) return null;
            if (!researchStates.ContainsKey(currentResearchId)) return null;

            var state = researchStates[currentResearchId];
            state.turnsRemaining--;

            if (state.turnsRemaining <= 0)
            {
                state.completed = true;
                Technology completed = allTechs[currentResearchId];
                currentResearchId = null;
                RecalculateBonuses();
                Debug.Log($"[TechTree] Research complete: {completed.name}!");
                return completed;
            }

            return null;
        }

        public bool IsUnitTypeUnlocked(UnitType unitType)
        {
            // Base units are always available
            if (unitType == UnitType.LineInfantry || unitType == UnitType.LightInfantry ||
                unitType == UnitType.Cavalry || unitType == UnitType.Artillery)
                return true;

            foreach (var kvp in researchStates)
            {
                if (!kvp.Value.completed) continue;
                if (!allTechs.ContainsKey(kvp.Key)) continue;
                Technology tech = allTechs[kvp.Key];
                if (tech.unlocksUnitType && tech.unlockedUnit == unitType)
                    return true;
            }
            return false;
        }

        public List<Technology> GetAvailableResearch()
        {
            List<Technology> available = new List<Technology>();
            foreach (var kvp in allTechs)
            {
                if (CanResearch(kvp.Key))
                    available.Add(kvp.Value);
            }
            return available;
        }

        public List<Technology> GetResearchedTechs()
        {
            List<Technology> researched = new List<Technology>();
            foreach (var kvp in researchStates)
            {
                if (kvp.Value.completed && allTechs.ContainsKey(kvp.Key))
                    researched.Add(allTechs[kvp.Key]);
            }
            return researched;
        }

        /// <summary>Count completed techs of a specific category (used for city visual era)</summary>
        public int GetCompletedCountByCategory(TechCategory category)
        {
            int count = 0;
            foreach (var kvp in researchStates)
            {
                if (!kvp.Value.completed) continue;
                if (!allTechs.ContainsKey(kvp.Key)) continue;
                if (allTechs[kvp.Key].category == category) count++;
            }
            return count;
        }

        /// <summary>
        /// Force complete a technology (used when research project finishes)
        /// </summary>
        public void ForceCompleteTech(string techId)
        {
            if (!allTechs.ContainsKey(techId)) return;
            if (IsResearched(techId)) return;
            
            researchStates[techId] = new TechResearchState
            {
                techId = techId,
                turnsRemaining = 0,
                completed = true
            };
            
            // Clear current research if this was it
            if (currentResearchId == techId)
            {
                currentResearchId = null;
            }
            
            RecalculateBonuses();
            
            // Apply to UnitTechnologySystem
            if (UnitTechnologySystem.Instance != null)
            {
                UnitTechnologySystem.Instance.OnTechnologyResearched(allTechs[techId]);
            }
            
            Debug.Log($"[TechTree] Force completed: {allTechs[techId].name}");
        }

        private void RecalculateBonuses()
        {
            TotalAccuracyBonus = 0f;
            TotalMoraleBonus = 0f;
            TotalSpeedBonus = 0f;
            TotalDamageBonus = 0f;
            TotalGoldIncomeBonus = 0f;
            TotalFoodBonus = 0f;
            TotalIronBonus = 0f;
            TotalRecruitCostReduction = 0f;
            TotalMaintenanceReduction = 0f;
            TotalDiplomacyBonus = 0f;

            foreach (var kvp in researchStates)
            {
                if (!kvp.Value.completed) continue;
                if (!allTechs.ContainsKey(kvp.Key)) continue;
                Technology tech = allTechs[kvp.Key];

                TotalAccuracyBonus += tech.accuracyBonus;
                TotalMoraleBonus += tech.moraleBonus;
                TotalSpeedBonus += tech.speedBonus;
                TotalDamageBonus += tech.damageBonus;
                TotalGoldIncomeBonus += tech.goldIncomeBonus;
                TotalFoodBonus += tech.foodBonus;
                TotalIronBonus += tech.ironBonus;
                TotalRecruitCostReduction += tech.recruitCostReduction;
                TotalMaintenanceReduction += tech.maintenanceReduction;
                TotalDiplomacyBonus += tech.diplomacyBonus;
            }
        }
    }
}
