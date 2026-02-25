using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    public enum CultureType
    {
        French, German, Austrian, Prussian, British, Spanish, Portuguese,
        Italian, Dutch, Polish, Russian, Swedish, Danish, Turkish, Greek,
        Walloon, Flemish, Bavarian, Saxon, Swiss, Hungarian, Czech, Croatian,
        Egyptian, Berber, Arab, Norman, Breton, Occitan, Catalan, Basque
    }

    public enum ReligionType
    {
        Catholic, Protestant, Orthodox, Islamic, Reformed, Anglican, Coptic
    }

    public enum ProvinceTerrainType
    {
        Plains,      // Plaines européennes (France, Pologne)
        Forest,      // Forêts denses (Allemagne, Scandinavie)
        Hills,       // Collines vallonnées (Écosse, Balkans)
        Mountains,   // Montagnes (Alpes, Pyrénées, Caucase)
        Desert,      // Aride (Égypte, Ottomans)
        Snow,        // Enneigé (Russie, Scandinavie)
        Coastal,     // Littoral (ports, falaises)
        Marsh,       // Marécages (Pays-Bas, Pologne)
        Urban        // Grande ville fortifiée
    }

    [System.Serializable]
    public class ProvinceData
    {
        public string provinceName;
        public string provinceId;
        public FactionType owner;
        public Vector2 mapPosition;
        public string[] neighborIds;

        // Terrain / Biome
        public ProvinceTerrainType terrainType = ProvinceTerrainType.Plains;

        // Resources
        public int population = 10000;
        public float goldIncome = 100f;
        public float foodProduction = 50f;
        public float ironProduction = 10f;

        // Culture & Religion
        public CultureType primaryCulture = CultureType.French;
        public ReligionType primaryReligion = ReligionType.Catholic;
        public float cultureAssimilation = 0f; // 0-1, progress toward owner's culture

        // Order & Unrest
        public float unrest = 0f;            // 0-100, high = revolts imminent
        public float loyalty = 80f;          // 0-100, how loyal to current owner
        public int turnsOccupied = 0;        // turns since last owner change
        public bool isOccupied = false;      // foreign occupation (not cored)
        public bool isCored = true;          // accepted as rightful territory
        public FactionType originalOwner;    // who owned it at game start

        // Population dynamics
        public float populationGrowth = 0f;  // calculated per turn
        public float devastation = 0f;       // 0-100, from battles/sieges/pillaging
        public float prosperity = 50f;       // 0-100, economic health

        // Revolt tracking
        public int revoltRisk = 0;           // calculated from unrest + other factors
        public bool hasActiveRevolt = false;
        public int revoltStrength = 0;       // rebel army size if revolting

        // Buildings
        public BuildingSlot[] buildings = new BuildingSlot[4];

        // Garrison
        public int garrisonSize = 0;

        // Recruitment
        public bool hasBarracks = false;
        public bool isCoastal = false;
        public bool hasStables = false;
        public bool hasArmory = false;

        public ProvinceData(string name, string id, FactionType owner, Vector2 pos, string[] neighbors)
        {
            provinceName = name;
            provinceId = id;
            this.owner = owner;
            mapPosition = pos;
            neighborIds = neighbors;

            for (int i = 0; i < buildings.Length; i++)
                buildings[i] = new BuildingSlot();

            originalOwner = owner;
        }

        /// <summary>Get the accepted culture for this province's owner faction</summary>
        public static CultureType GetFactionCulture(FactionType faction) => faction switch
        {
            FactionType.France => CultureType.French,
            FactionType.Britain => CultureType.British,
            FactionType.Prussia => CultureType.Prussian,
            FactionType.Russia => CultureType.Russian,
            FactionType.Austria => CultureType.Austrian,
            FactionType.Spain => CultureType.Spanish,
            FactionType.Ottoman => CultureType.Turkish,
            FactionType.Portugal => CultureType.Portuguese,
            FactionType.Sweden => CultureType.Swedish,
            FactionType.Denmark => CultureType.Danish,
            FactionType.Poland => CultureType.Polish,
            FactionType.Venice => CultureType.Italian,
            FactionType.Dutch => CultureType.Dutch,
            FactionType.Bavaria => CultureType.Bavarian,
            FactionType.Saxony => CultureType.Saxon,
            FactionType.PapalStates => CultureType.Italian,
            FactionType.Savoy => CultureType.Italian,
            FactionType.Switzerland => CultureType.Swiss,
            FactionType.Genoa => CultureType.Italian,
            FactionType.Tuscany => CultureType.Italian,
            FactionType.Hanover => CultureType.German,
            FactionType.Modena => CultureType.Italian,
            FactionType.Parma => CultureType.Italian,
            FactionType.Lorraine => CultureType.French,
            _ => CultureType.French
        };

        /// <summary>Get the accepted religion for this province's owner faction</summary>
        public static ReligionType GetFactionReligion(FactionType faction) => faction switch
        {
            FactionType.France => ReligionType.Catholic,
            FactionType.Britain => ReligionType.Anglican,
            FactionType.Prussia => ReligionType.Protestant,
            FactionType.Russia => ReligionType.Orthodox,
            FactionType.Austria => ReligionType.Catholic,
            FactionType.Spain => ReligionType.Catholic,
            FactionType.Ottoman => ReligionType.Islamic,
            FactionType.Portugal => ReligionType.Catholic,
            FactionType.Sweden => ReligionType.Protestant,
            FactionType.Denmark => ReligionType.Protestant,
            FactionType.Poland => ReligionType.Catholic,
            FactionType.Venice => ReligionType.Catholic,
            FactionType.Dutch => ReligionType.Reformed,
            FactionType.Bavaria => ReligionType.Catholic,
            FactionType.Saxony => ReligionType.Protestant,
            FactionType.PapalStates => ReligionType.Catholic,
            FactionType.Savoy => ReligionType.Catholic,
            FactionType.Switzerland => ReligionType.Reformed,
            FactionType.Genoa => ReligionType.Catholic,
            FactionType.Tuscany => ReligionType.Catholic,
            FactionType.Hanover => ReligionType.Protestant,
            FactionType.Modena => ReligionType.Catholic,
            FactionType.Parma => ReligionType.Catholic,
            FactionType.Lorraine => ReligionType.Catholic,
            _ => ReligionType.Catholic
        };

        /// <summary>Is this province's culture accepted by the owner?</summary>
        public bool IsCultureAccepted => primaryCulture == GetFactionCulture(owner);

        /// <summary>Is this province's religion the same as the owner's?</summary>
        public bool IsReligionAccepted => primaryReligion == GetFactionReligion(owner);

        /// <summary>Process one turn of province dynamics</summary>
        public void ProcessProvinceTurn(FactionData ownerFaction)
        {
            ProcessPopulationGrowth(ownerFaction);
            ProcessUnrest(ownerFaction);
            ProcessCultureAssimilation(ownerFaction);
            ProcessDevastationRecovery();
            ProcessProsperity();
            ProcessOccupation();
            CalculateRevoltRisk();
        }

        private void ProcessPopulationGrowth(FactionData ownerFaction)
        {
            // Base growth: 0.5% per turn, reduced by devastation, war, high conscription
            float baseGrowth = 0.005f;

            // Devastation reduces growth (war damage)
            float devMod = 1f - (devastation / 100f);

            // Food surplus increases growth
            float foodMod = ownerFaction.food > 0 ? 1.1f : 0.7f;

            // High unrest reduces growth (people flee)
            float unrestMod = 1f - (unrest / 200f);

            // Prosperity bonus
            float prospMod = 0.8f + (prosperity / 250f);

            populationGrowth = baseGrowth * devMod * foodMod * unrestMod * prospMod;

            // Apply growth
            int growth = Mathf.RoundToInt(population * populationGrowth);
            population = Mathf.Max(500, population + growth);

            // Cap population based on terrain
            int maxPop = terrainType switch
            {
                ProvinceTerrainType.Desert => 15000,
                ProvinceTerrainType.Snow => 20000,
                ProvinceTerrainType.Mountains => 25000,
                ProvinceTerrainType.Marsh => 20000,
                ProvinceTerrainType.Urban => 150000,
                ProvinceTerrainType.Coastal => 80000,
                _ => 60000
            };

            // Buildings increase cap
            foreach (var b in buildings)
            {
                if (b.type == BuildingType.Farm) maxPop += 5000 * b.level;
                if (b.type == BuildingType.Market) maxPop += 3000 * b.level;
            }

            population = Mathf.Min(population, maxPop);
        }

        private void ProcessUnrest(FactionData ownerFaction)
        {
            float unrestDelta = 0f;

            // Foreign culture adds unrest
            if (!IsCultureAccepted) unrestDelta += 2f;

            // Foreign religion adds unrest
            if (!IsReligionAccepted) unrestDelta += 1.5f;

            // Occupation (not cored) adds heavy unrest
            if (isOccupied || !isCored) unrestDelta += 3f;

            // High devastation adds unrest
            unrestDelta += devastation * 0.05f;

            // Low prosperity adds unrest
            if (prosperity < 30f) unrestDelta += 1.5f;

            // High conscription adds unrest
            float conscMod = EquipmentSystem.GetConscriptionPercent(ownerFaction.conscriptionLaw);
            if (conscMod > 0.05f) unrestDelta += (conscMod - 0.05f) * 20f;

            // Garrison reduces unrest
            if (garrisonSize > 0) unrestDelta -= Mathf.Min(5f, garrisonSize * 0.01f);

            // Church reduces unrest (especially religious unrest)
            foreach (var b in buildings)
            {
                if (b.type == BuildingType.Church) unrestDelta -= 2f * b.level;
                if (b.type == BuildingType.Fortress) unrestDelta -= 1f * b.level;
            }

            // High loyalty counters unrest
            if (loyalty > 60f) unrestDelta -= 1f;

            // Natural decay toward 0
            if (unrest > 0) unrestDelta -= 0.5f;

            unrest = Mathf.Clamp(unrest + unrestDelta, 0f, 100f);
        }

        private void ProcessCultureAssimilation(FactionData ownerFaction)
        {
            if (IsCultureAccepted) { cultureAssimilation = 1f; return; }

            // Base assimilation: very slow (0.2% per turn)
            float rate = 0.002f;

            // Church accelerates assimilation
            foreach (var b in buildings)
                if (b.type == BuildingType.Church) rate += 0.001f * b.level;

            // University helps too
            foreach (var b in buildings)
                if (b.type == BuildingType.University) rate += 0.002f * b.level;

            // High unrest slows assimilation
            rate *= (1f - unrest / 200f);

            cultureAssimilation = Mathf.Clamp01(cultureAssimilation + rate);

            // After full assimilation, convert culture
            if (cultureAssimilation >= 1f)
            {
                primaryCulture = GetFactionCulture(owner);
                cultureAssimilation = 1f;
            }
        }

        private void ProcessDevastationRecovery()
        {
            // Devastation slowly recovers
            if (devastation > 0)
            {
                float recovery = 1f; // 1% per turn base

                // Prosperity helps recovery
                recovery += prosperity * 0.01f;

                devastation = Mathf.Max(0f, devastation - recovery);
            }
        }

        private void ProcessProsperity()
        {
            float delta = 0f;

            // Market buildings boost prosperity
            foreach (var b in buildings)
            {
                if (b.type == BuildingType.Market) delta += 1f * b.level;
                if (b.type == BuildingType.Farm) delta += 0.5f * b.level;
            }

            // Devastation kills prosperity
            delta -= devastation * 0.1f;

            // Unrest reduces prosperity
            delta -= unrest * 0.05f;

            // Natural drift toward 50
            float drift = (50f - prosperity) * 0.02f;
            delta += drift;

            prosperity = Mathf.Clamp(prosperity + delta, 0f, 100f);
        }

        private void ProcessOccupation()
        {
            if (isOccupied || !isCored)
            {
                turnsOccupied++;

                // After 20 turns of occupation, province becomes cored
                if (turnsOccupied >= 20 && !isCored)
                {
                    isCored = true;
                    isOccupied = false;
                    loyalty = 30f; // Low initial loyalty for new core
                }
            }
            else
            {
                // Loyalty slowly increases for cored provinces
                loyalty = Mathf.Min(100f, loyalty + 0.5f);

                // Foreign culture slows loyalty gain
                if (!IsCultureAccepted) loyalty = Mathf.Min(100f, loyalty - 0.2f);
            }
        }

        private void CalculateRevoltRisk()
        {
            // Revolt risk = unrest weighted by various factors
            float risk = unrest;

            // Foreign culture increases risk
            if (!IsCultureAccepted) risk += 10f;

            // Foreign religion increases risk
            if (!IsReligionAccepted) risk += 5f;

            // Occupation increases risk
            if (isOccupied) risk += 15f;

            // Low loyalty increases risk
            risk += (100f - loyalty) * 0.2f;

            // Garrison reduces risk
            risk -= garrisonSize * 0.02f;

            revoltRisk = Mathf.Clamp((int)risk, 0, 100);
        }

        /// <summary>Check and spawn revolt. Returns rebel army size or 0.</summary>
        public int CheckRevolt()
        {
            if (hasActiveRevolt) return 0;
            if (revoltRisk < 60) return 0;

            // Probability: (revoltRisk - 60) / 100 per turn
            float chance = (revoltRisk - 60f) / 100f;
            if (Random.value > chance) return 0;

            // Revolt! Rebel army size based on population and unrest
            hasActiveRevolt = true;
            revoltStrength = Mathf.Clamp(population / 20, 100, 5000);

            Debug.Log($"[Province] REVOLT in {provinceName}! {revoltStrength} rebels rise up!");
            return revoltStrength;
        }

        /// <summary>Apply battle devastation to province</summary>
        public void ApplyBattleDevastation(float amount)
        {
            devastation = Mathf.Clamp(devastation + amount, 0f, 100f);
            prosperity = Mathf.Max(0f, prosperity - amount * 0.5f);

            // Battle kills some population
            int casualties = Mathf.RoundToInt(population * amount * 0.001f);
            population = Mathf.Max(500, population - casualties);
        }

        /// <summary>Transfer province to new owner</summary>
        public void TransferOwnership(FactionType newOwner)
        {
            FactionType oldOwner = owner;
            owner = newOwner;
            isOccupied = true;
            isCored = false;
            turnsOccupied = 0;
            loyalty = 10f;  // Very low loyalty after conquest
            unrest += 20f;
            unrest = Mathf.Min(unrest, 100f);
            cultureAssimilation = 0f;
        }
    }

    [System.Serializable]
    public class BuildingSlot
    {
        public BuildingType type = BuildingType.Empty;
        public int level = 0;
        public bool isConstructing = false;
        public int turnsToComplete = 0;
    }

    public enum BuildingType
    {
        Empty,
        Farm,
        Mine,
        Barracks,
        Stables,
        Armory,
        Fortress,
        Market,
        Church,
        University,
        
        // Research buildings (tiered)
        SmallArtillerySchool,       // 2 slots, basic research
        ProvincialArtillerySchool,  // 4 slots, +10% efficiency
        RoyalArtilleryAcademy,      // 6 slots, +20% efficiency
        GrandArtilleryAcademy,      // 8 slots, +30% efficiency
        ImperialArtilleryAcademy,   // 10 slots, +40% efficiency
        
        // Military buildings (tiered)
        VillageBarracks,            // Basic training
        ProvincialBarracks,         // Better training
        MilitaryAcademy,            // Officer training
        RoyalMilitaryCollege,       // Elite training
        MilitaryUniversity          // Premier institution
    }

    public static class BuildingInfo
    {
        public static string GetName(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Farm: return "Farm";
                case BuildingType.Mine: return "Iron Mine";
                case BuildingType.Barracks: return "Barracks";
                case BuildingType.Stables: return "Stables";
                case BuildingType.Armory: return "Armory";
                case BuildingType.Fortress: return "Fortress";
                case BuildingType.Market: return "Market";
                case BuildingType.Church: return "Church";
                case BuildingType.University: return "University";
                
                // Research buildings
                case BuildingType.SmallArtillerySchool: return "Small Artillery School";
                case BuildingType.ProvincialArtillerySchool: return "Provincial Artillery School";
                case BuildingType.RoyalArtilleryAcademy: return "Royal Artillery Academy";
                case BuildingType.GrandArtilleryAcademy: return "Grand Artillery Academy";
                case BuildingType.ImperialArtilleryAcademy: return "Imperial Artillery Academy";
                
                // Military buildings
                case BuildingType.VillageBarracks: return "Village Barracks";
                case BuildingType.ProvincialBarracks: return "Provincial Barracks";
                case BuildingType.MilitaryAcademy: return "Military Academy";
                case BuildingType.RoyalMilitaryCollege: return "Royal Military College";
                case BuildingType.MilitaryUniversity: return "Military University";
                
                default: return "Empty";
            }
        }

        public static string GetDescription(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Farm: return "Cultivated fields providing sustenance for the population. Each level increases food output.";
                case BuildingType.Mine: return "Iron ore extraction facility. Supplies raw materials for weapons, armor, and construction.";
                case BuildingType.Barracks: return "Training grounds for infantry regiments. Required to recruit Line Infantry, Light Infantry, and Grenadiers.";
                case BuildingType.Stables: return "Horse breeding and cavalry training facility. Required to recruit Cavalry, Hussars, and Lancers.";
                case BuildingType.Armory: return "Weapons forge and ammunition depot. Required to recruit Artillery units. Boosts unit damage.";
                case BuildingType.Fortress: return "Massive defensive fortifications with walls and bastions. Increases garrison capacity and city defense.";
                case BuildingType.Market: return "Trade hub attracting merchants and commerce. Generates gold income and attracts bourgeois population.";
                case BuildingType.Church: return "Place of worship providing spiritual guidance. Improves public order and provides modest research bonus.";
                case BuildingType.University: return "Center of learning and scientific advancement. Significantly boosts research speed and attracts educated citizens.";
                case BuildingType.SmallArtillerySchool: return "A modest school teaching basic gunnery. Improves artillery crew competence.";
                case BuildingType.ProvincialArtillerySchool: return "Regional artillery training institution. Develops standardized firing procedures.";
                case BuildingType.RoyalArtilleryAcademy: return "Premier institution for artillery science, ballistics, and fortification engineering.";
                case BuildingType.GrandArtilleryAcademy: return "World-class artillery academy producing the finest gunners on the continent.";
                case BuildingType.ImperialArtilleryAcademy: return "The pinnacle of artillery education. Graduates are masters of siege warfare and field bombardment.";
                case BuildingType.VillageBarracks: return "Simple training grounds for local militia and basic infantry.";
                case BuildingType.ProvincialBarracks: return "Professional training facility producing disciplined soldiers.";
                case BuildingType.MilitaryAcademy: return "Officer training institution fostering tactical and strategic thinking.";
                case BuildingType.RoyalMilitaryCollege: return "Elite institution training the finest officers in the realm.";
                case BuildingType.MilitaryUniversity: return "The highest level of military education, producing strategic masterminds.";
                default: return "Empty building slot.";
            }
        }

        public static string GetIcon(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Farm: return "🌾";
                case BuildingType.Mine: return "⛏";
                case BuildingType.Barracks: return "⚔";
                case BuildingType.Stables: return "🏇";
                case BuildingType.Armory: return "💣";
                case BuildingType.Fortress: return "🏰";
                case BuildingType.Market: return "💰";
                case BuildingType.Church: return "⛪";
                case BuildingType.University: return "🏛";
                case BuildingType.SmallArtillerySchool: return "🎯";
                case BuildingType.ProvincialArtillerySchool: return "🎯";
                case BuildingType.RoyalArtilleryAcademy: return "🎯";
                case BuildingType.GrandArtilleryAcademy: return "🎯";
                case BuildingType.ImperialArtilleryAcademy: return "🎯";
                case BuildingType.VillageBarracks: return "🎖";
                case BuildingType.ProvincialBarracks: return "🎖";
                case BuildingType.MilitaryAcademy: return "🎖";
                case BuildingType.RoyalMilitaryCollege: return "🎖";
                case BuildingType.MilitaryUniversity: return "🎖";
                default: return "❓";
            }
        }

        public static BuildingCategory GetCategory(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Farm:
                case BuildingType.Mine:
                case BuildingType.Market:
                    return BuildingCategory.Economy;
                case BuildingType.Barracks:
                case BuildingType.Stables:
                case BuildingType.Armory:
                case BuildingType.Fortress:
                case BuildingType.SmallArtillerySchool:
                case BuildingType.ProvincialArtillerySchool:
                case BuildingType.RoyalArtilleryAcademy:
                case BuildingType.GrandArtilleryAcademy:
                case BuildingType.ImperialArtilleryAcademy:
                case BuildingType.VillageBarracks:
                case BuildingType.ProvincialBarracks:
                    return BuildingCategory.Military;
                case BuildingType.Church:
                    return BuildingCategory.Religion;
                case BuildingType.University:
                case BuildingType.MilitaryAcademy:
                case BuildingType.RoyalMilitaryCollege:
                case BuildingType.MilitaryUniversity:
                    return BuildingCategory.Academic;
                default:
                    return BuildingCategory.Infrastructure;
            }
        }

        public static string GetEffects(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Farm: return "+25 Food/turn per level";
                case BuildingType.Mine: return "+15 Iron/turn per level";
                case BuildingType.Barracks: return "Recruits: Line Infantry, Light Infantry, Grenadiers";
                case BuildingType.Stables: return "Recruits: Cavalry, Hussars, Lancers";
                case BuildingType.Armory: return "Recruits: Artillery  •  +10% unit damage";
                case BuildingType.Fortress: return "+1 Fortification level  •  +500 garrison capacity";
                case BuildingType.Market: return "+20 Gold/turn  •  +0.5% bourgeois growth";
                case BuildingType.Church: return "+5 Public Order  •  +10% research speed";
                case BuildingType.University: return "+25% Research Speed  •  Bourgeois & noble growth";
                case BuildingType.SmallArtillerySchool: return "+10% artillery efficiency";
                case BuildingType.ProvincialArtillerySchool: return "+20% artillery efficiency";
                case BuildingType.RoyalArtilleryAcademy: return "+30% artillery efficiency";
                case BuildingType.GrandArtilleryAcademy: return "+35% artillery efficiency";
                case BuildingType.ImperialArtilleryAcademy: return "+40% artillery efficiency";
                case BuildingType.VillageBarracks: return "Basic training  •  -5% recruit cost";
                case BuildingType.ProvincialBarracks: return "Professional training  •  -10% recruit cost";
                case BuildingType.MilitaryAcademy: return "Officer training  •  +10% all unit stats";
                case BuildingType.RoyalMilitaryCollege: return "Elite training  •  +15% all unit stats";
                case BuildingType.MilitaryUniversity: return "Supreme training  •  +20% all unit stats";
                default: return "No effects";
            }
        }

        public static int GetCostGold(BuildingType type, int level)
        {
            int baseCost;
            switch (type)
            {
                case BuildingType.Farm: baseCost = 200; break;
                case BuildingType.Mine: baseCost = 300; break;
                case BuildingType.Barracks: baseCost = 400; break;
                case BuildingType.Stables: baseCost = 500; break;
                case BuildingType.Armory: baseCost = 600; break;
                case BuildingType.Fortress: baseCost = 800; break;
                case BuildingType.Market: baseCost = 350; break;
                case BuildingType.Church: baseCost = 250; break;
                case BuildingType.University: baseCost = 500; break;
                
                // Research buildings
                case BuildingType.SmallArtillerySchool: baseCost = 400; break;
                case BuildingType.ProvincialArtillerySchool: baseCost = 800; break;
                case BuildingType.RoyalArtilleryAcademy: baseCost = 1500; break;
                case BuildingType.GrandArtilleryAcademy: baseCost = 2500; break;
                case BuildingType.ImperialArtilleryAcademy: baseCost = 4000; break;
                
                // Military buildings
                case BuildingType.VillageBarracks: baseCost = 300; break;
                case BuildingType.ProvincialBarracks: baseCost = 600; break;
                case BuildingType.MilitaryAcademy: baseCost = 1200; break;
                case BuildingType.RoyalMilitaryCollege: baseCost = 2000; break;
                case BuildingType.MilitaryUniversity: baseCost = 3500; break;
                
                default: return 0;
            }
            return baseCost * (level + 1);
        }

        public static int GetBuildTime(BuildingType type)
        {
            switch (type)
            {
                // Basic buildings — ~10-15 days
                case BuildingType.Farm: return 10;
                case BuildingType.Church: return 12;
                case BuildingType.VillageBarracks: return 10;
                
                // Mid-tier — ~15-20 days
                case BuildingType.Mine: return 15;
                case BuildingType.Market: return 15;
                case BuildingType.Barracks: return 18;
                case BuildingType.ProvincialBarracks: return 20;
                case BuildingType.SmallArtillerySchool: return 18;
                
                // Advanced — ~20-30 days
                case BuildingType.Stables: return 22;
                case BuildingType.Armory: return 25;
                case BuildingType.University: return 28;
                case BuildingType.ProvincialArtillerySchool: return 25;
                case BuildingType.MilitaryAcademy: return 28;
                
                // High-tier — ~30-40 days
                case BuildingType.Fortress: return 35;
                case BuildingType.RoyalArtilleryAcademy: return 32;
                case BuildingType.RoyalMilitaryCollege: return 35;
                case BuildingType.GrandArtilleryAcademy: return 38;
                
                // Top-tier — ~40-45 days  
                case BuildingType.ImperialArtilleryAcademy: return 45;
                case BuildingType.MilitaryUniversity: return 42;
                
                default: return 10;
            }
        }
    }
}
