using System.Collections.Generic;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    [System.Serializable]
    public class FactionData
    {
        public FactionType factionType;
        public string factionName;
        
        // === BASIC RESOURCES ===
        public float gold = 1000f;
        public float food = 500f;
        public float iron = 200f;

        // === HOI4 RESOURCES ===
        public int manpower = 50000;           // Available manpower pool
        public int maxManpower = 120000;       // Total population-based manpower
        public float politicalPower = 100f;    // PP — generated per turn, spent on decisions
        public float politicalPowerGain = 2f;  // PP gain per turn (base)
        public float stability = 0.70f;        // 0.0 to 1.0 — affects production & order
        public float warSupport = 0.50f;       // 0.0 to 1.0 — affects recruitment & morale

        // === EQUIPMENT ===
        public EquipmentStockpile equipment = new EquipmentStockpile();

        // === NATIONAL LAWS ===
        public ConscriptionLaw conscriptionLaw = ConscriptionLaw.LimitedConscription;
        public EconomyLaw economyLaw = EconomyLaw.CivilianEconomy;
        public TradeLaw tradeLaw = TradeLaw.LimitedExports;

        // === PRODUCTION ===
        public int civilianFactories = 5;      // Build buildings
        public int militaryFactories = 3;      // Produce equipment
        public int navalYards = 0;             // Build ships
        
        public List<string> ownedProvinceIds = new List<string>();
        public List<string> armyIds = new List<string>();

        // Diplomacy
        public Dictionary<FactionType, DiplomaticRelation> relations = new Dictionary<FactionType, DiplomaticRelation>();

        public bool isPlayerControlled = false;
        public bool isEliminated = false;

        // Tech tree
        public TechTree techTree;

        public FactionData(FactionType type, string name, bool isPlayer = false)
        {
            factionType = type;
            factionName = name;
            isPlayerControlled = isPlayer;
            techTree = new TechTree();
            equipment = new EquipmentStockpile();
        }

        public void AddIncome(float goldIncome, float foodIncome, float ironIncome)
        {
            gold += goldIncome;
            food += foodIncome;
            iron += ironIncome;
        }

        public bool CanAfford(float goldCost, float ironCost = 0f)
        {
            return gold >= goldCost && iron >= ironCost;
        }

        public void Spend(float goldCost, float ironCost = 0f)
        {
            gold -= goldCost;
            iron -= ironCost;
        }

        /// <summary>Process per-turn resource gains (PP, stability drift, manpower recovery)</summary>
        public void ProcessTurnResources()
        {
            var diff = DifficultyManager.Instance;

            // Political power (modified by difficulty)
            politicalPower += politicalPowerGain * diff.PoliticalPowerGainMultiplier;

            // Stability drifts toward base (0.5) slowly — decay rate affected by difficulty
            float stabilityTarget = 0.50f 
                + EquipmentSystem.GetConscriptionStabilityPenalty(conscriptionLaw)
                + EquipmentSystem.GetEconomyStabilityPenalty(economyLaw);
            float stabilitySpeed = 0.01f * diff.StabilityDecayRate;
            stability = UnityEngine.Mathf.MoveTowards(stability, stabilityTarget, stabilitySpeed);
            stability = UnityEngine.Mathf.Clamp01(stability);

            // Manpower recovery (based on conscription law and owned provinces)
            float conscriptionPct = EquipmentSystem.GetConscriptionPercent(conscriptionLaw);
            maxManpower = (int)(ownedProvinceIds.Count * 8000 * conscriptionPct / 0.025f);
            int mpGain = UnityEngine.Mathf.Max(100, maxManpower / 20);
            mpGain = (int)(mpGain * diff.ReinforcementRateMultiplier);
            manpower = UnityEngine.Mathf.Min(manpower + mpGain, maxManpower);

            // War support drifts down slowly in peacetime — decay rate affected by difficulty
            float wsDecay = 0.005f * diff.WarSupportDecayRate;
            warSupport = UnityEngine.Mathf.MoveTowards(warSupport, 0.30f, wsDecay);
            warSupport = UnityEngine.Mathf.Clamp01(warSupport);
        }

        /// <summary>Consume manpower for recruitment. Returns true if enough available.</summary>
        public bool ConsumeManpower(int amount)
        {
            if (manpower >= amount)
            {
                manpower -= amount;
                return true;
            }
            return false;
        }

        /// <summary>Spend political power. Returns true if enough available.</summary>
        public bool SpendPoliticalPower(float amount)
        {
            if (politicalPower >= amount)
            {
                politicalPower -= amount;
                return true;
            }
            return false;
        }
    }

    [System.Serializable]
    public class DiplomaticRelation
    {
        public DiplomacyState state = DiplomacyState.Neutral;
        public int relationScore = 0;
        public int turnsAtWar = 0;
        public int turnsOfAlliance = 0;

        public DiplomaticRelation() { }

        public DiplomaticRelation(DiplomacyState state)
        {
            this.state = state;
        }
    }

    public enum DiplomacyState
    {
        War,
        Hostile,
        Neutral,
        Friendly,
        Alliance
    }
}
