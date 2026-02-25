using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    // ==================== EQUIPMENT TYPES ====================
    public enum EquipmentType
    {
        Muskets,
        Bayonets,
        Sabres,
        CannonsLight,
        CannonsHeavy,
        CannonsSiege,
        Horses,
        Uniforms,
        Gunpowder,
        Cannonballs
    }

    // ==================== CONSCRIPTION LAWS ====================
    public enum ConscriptionLaw
    {
        Volunteer,          // 1% of population → manpower
        LimitedConscription,// 2.5%
        ExtendedConscription,// 5%
        ServiceByRequirement,// 8%
        TotalMobilization    // 15% — heavy stability penalty
    }

    // ==================== ECONOMY LAWS ====================
    public enum EconomyLaw
    {
        CivilianEconomy,    // No bonuses
        PreMobilization,    // +10% factory output, -5% stability
        WarEconomy,         // +25% factory output, -10% stability
        TotalWar            // +50% factory output, -20% stability
    }

    // ==================== TRADE LAWS ====================
    public enum TradeLaw
    {
        FreeTradePolicy,    // +15% research, -20% resources
        ExportFocus,        // +5% research, -10% resources
        LimitedExports,     // No modifiers
        ClosedEconomy       // -5% research, +20% resources
    }

    // ==================== EQUIPMENT STOCKPILE ====================
    [System.Serializable]
    public class EquipmentStockpile
    {
        private Dictionary<EquipmentType, float> stock = new Dictionary<EquipmentType, float>();

        public EquipmentStockpile()
        {
            foreach (EquipmentType t in System.Enum.GetValues(typeof(EquipmentType)))
                stock[t] = 0f;
        }

        public float Get(EquipmentType type) => stock.ContainsKey(type) ? stock[type] : 0f;
        public void Add(EquipmentType type, float amount) => stock[type] = Get(type) + amount;
        
        public bool TryConsume(EquipmentType type, float amount)
        {
            float current = Get(type);
            if (current >= amount)
            {
                stock[type] = current - amount;
                return true;
            }
            return false;
        }

        public void Consume(EquipmentType type, float amount)
        {
            stock[type] = Mathf.Max(0f, Get(type) - amount);
        }

        public float GetDeficitRatio(EquipmentType type, float demand)
        {
            if (demand <= 0f) return 1f;
            return Mathf.Clamp01(Get(type) / demand);
        }

        public Dictionary<EquipmentType, float> GetAll() => new Dictionary<EquipmentType, float>(stock);
    }

    // ==================== REGIMENT EQUIPMENT NEEDS ====================
    [System.Serializable]
    public class RegimentEquipmentNeeds
    {
        public Dictionary<EquipmentType, int> requirements = new Dictionary<EquipmentType, int>();

        public static RegimentEquipmentNeeds ForUnitType(UnitType type, int size)
        {
            var needs = new RegimentEquipmentNeeds();
            switch (type)
            {
                // ── INFANTRY ──────────────────────────────────────
                case UnitType.Militia:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.3f);
                    break;
                case UnitType.TrainedMilitia:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.4f);
                    needs.requirements[EquipmentType.Uniforms] = (int)(size * 0.5f);
                    break;
                case UnitType.LineInfantry:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.5f);
                    break;
                case UnitType.LightInfantry:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = (int)(size * 0.5f);
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.6f);
                    break;
                case UnitType.Fusilier:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.7f);
                    break;
                case UnitType.Grenadier:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.8f);
                    break;
                case UnitType.Voltigeur:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = (int)(size * 0.6f);
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.8f);
                    break;
                case UnitType.Chasseur:
                    needs.requirements[EquipmentType.Muskets] = size;           // Rifles
                    needs.requirements[EquipmentType.Bayonets] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = size;         // Rifles use more powder
                    break;
                case UnitType.GuardInfantry:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = size;
                    break;
                case UnitType.OldGuard:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 1.2f);
                    break;

                // ── CAVALRY ──────────────────────────────────────
                case UnitType.MilitiaCavalry:
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    break;
                case UnitType.Dragoon:
                    needs.requirements[EquipmentType.Muskets] = size;    // Dragoons fight dismounted too
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.3f);
                    break;
                case UnitType.Cavalry:
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    break;
                case UnitType.Hussar:
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    break;
                case UnitType.Lancer:
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    break;
                case UnitType.Cuirassier:
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    break;
                case UnitType.GuardCavalry:
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    break;
                case UnitType.Mameluke:
                    needs.requirements[EquipmentType.Sabres] = size;
                    needs.requirements[EquipmentType.Horses] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    break;

                // ── ARTILLERY ─────────────────────────────────────
                case UnitType.GarrisonCannon:
                    needs.requirements[EquipmentType.CannonsLight] = size;
                    needs.requirements[EquipmentType.Gunpowder] = size * 2;
                    needs.requirements[EquipmentType.Cannonballs] = size * 3;
                    break;
                case UnitType.Artillery:
                    needs.requirements[EquipmentType.CannonsLight] = (int)(size * 0.6f);
                    needs.requirements[EquipmentType.CannonsHeavy] = (int)(size * 0.4f);
                    needs.requirements[EquipmentType.Gunpowder] = size * 2;
                    needs.requirements[EquipmentType.Cannonballs] = size * 3;
                    needs.requirements[EquipmentType.Horses] = size * 2;
                    break;
                case UnitType.HorseArtillery:
                    needs.requirements[EquipmentType.CannonsLight] = size;
                    needs.requirements[EquipmentType.Gunpowder] = size * 2;
                    needs.requirements[EquipmentType.Cannonballs] = size * 3;
                    needs.requirements[EquipmentType.Horses] = size * 3;
                    break;
                case UnitType.Howitzer:
                    needs.requirements[EquipmentType.CannonsHeavy] = size;
                    needs.requirements[EquipmentType.Gunpowder] = size * 3;
                    needs.requirements[EquipmentType.Cannonballs] = size * 4;
                    needs.requirements[EquipmentType.Horses] = size * 2;
                    break;
                case UnitType.GrandBattery:
                    needs.requirements[EquipmentType.CannonsHeavy] = size;
                    needs.requirements[EquipmentType.CannonsLight] = (int)(size * 0.5f);
                    needs.requirements[EquipmentType.Gunpowder] = size * 4;
                    needs.requirements[EquipmentType.Cannonballs] = size * 5;
                    needs.requirements[EquipmentType.Horses] = size * 3;
                    break;
                case UnitType.GuardArtillery:
                    needs.requirements[EquipmentType.CannonsHeavy] = size;
                    needs.requirements[EquipmentType.Gunpowder] = size * 4;
                    needs.requirements[EquipmentType.Cannonballs] = size * 5;
                    needs.requirements[EquipmentType.Horses] = size * 3;
                    break;

                // ── SPECIAL UNITS ─────────────────────────────────
                case UnitType.Engineer:
                case UnitType.Sapper:
                    needs.requirements[EquipmentType.Muskets] = (int)(size * 0.5f);
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = size;     // Explosives
                    break;
                case UnitType.Marine:
                    needs.requirements[EquipmentType.Muskets] = size;
                    needs.requirements[EquipmentType.Bayonets] = size;
                    needs.requirements[EquipmentType.Uniforms] = size;
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.6f);
                    break;
                case UnitType.Partisan:
                    needs.requirements[EquipmentType.Muskets] = (int)(size * 0.7f);
                    needs.requirements[EquipmentType.Gunpowder] = (int)(size * 0.3f);
                    break;
            }
            return needs;
        }

        /// <summary>Per-turn attrition consumption (wear and tear)</summary>
        public static Dictionary<EquipmentType, float> AttritionPerTurn(UnitType type, int size)
        {
            var attrition = new Dictionary<EquipmentType, float>();
            float ratio = size / 200f; // Normalize to standard regiment size
            switch (type)
            {
                case UnitType.Militia:
                case UnitType.TrainedMilitia:
                    attrition[EquipmentType.Gunpowder] = 3f * ratio;
                    break;
                case UnitType.LineInfantry:
                case UnitType.LightInfantry:
                case UnitType.Fusilier:
                    attrition[EquipmentType.Gunpowder] = 5f * ratio;
                    attrition[EquipmentType.Uniforms] = 1f * ratio;
                    break;
                case UnitType.Grenadier:
                case UnitType.Voltigeur:
                case UnitType.Chasseur:
                case UnitType.GuardInfantry:
                case UnitType.OldGuard:
                    attrition[EquipmentType.Gunpowder] = 8f * ratio;
                    attrition[EquipmentType.Uniforms] = 1.5f * ratio;
                    break;
                case UnitType.MilitiaCavalry:
                    attrition[EquipmentType.Horses] = 1f * ratio;
                    break;
                case UnitType.Dragoon:
                    attrition[EquipmentType.Horses] = 2f * ratio;
                    attrition[EquipmentType.Uniforms] = 1f * ratio;
                    attrition[EquipmentType.Gunpowder] = 2f * ratio;
                    break;
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                case UnitType.Cuirassier:
                case UnitType.GuardCavalry:
                case UnitType.Mameluke:
                    attrition[EquipmentType.Horses] = 2f * ratio;
                    attrition[EquipmentType.Uniforms] = 1f * ratio;
                    break;
                case UnitType.GarrisonCannon:
                    attrition[EquipmentType.Gunpowder] = 4f * ratio;
                    attrition[EquipmentType.Cannonballs] = 3f * ratio;
                    break;
                case UnitType.Artillery:
                case UnitType.HorseArtillery:
                case UnitType.Howitzer:
                case UnitType.GrandBattery:
                case UnitType.GuardArtillery:
                    attrition[EquipmentType.Gunpowder] = 10f * ratio;
                    attrition[EquipmentType.Cannonballs] = 8f * ratio;
                    attrition[EquipmentType.Horses] = 1f * ratio;
                    break;
                case UnitType.Engineer:
                case UnitType.Sapper:
                    attrition[EquipmentType.Gunpowder] = 6f * ratio;
                    break;
                case UnitType.Marine:
                    attrition[EquipmentType.Gunpowder] = 4f * ratio;
                    attrition[EquipmentType.Uniforms] = 1f * ratio;
                    break;
                case UnitType.Partisan:
                    attrition[EquipmentType.Gunpowder] = 2f * ratio;
                    break;
            }
            return attrition;
        }
    }

    // ==================== EQUIPMENT SYSTEM (per-faction manager) ====================
    public static class EquipmentSystem
    {
        /// <summary>Calculate total equipment demand for a faction's armies</summary>
        public static Dictionary<EquipmentType, float> CalculateTotalDemand(FactionData faction, Dictionary<string, ArmyData> armies)
        {
            var demand = new Dictionary<EquipmentType, float>();
            foreach (EquipmentType t in System.Enum.GetValues(typeof(EquipmentType)))
                demand[t] = 0f;

            foreach (string armyId in faction.armyIds)
            {
                if (!armies.ContainsKey(armyId)) continue;
                var army = armies[armyId];
                foreach (var reg in army.regiments)
                {
                    var needs = RegimentEquipmentNeeds.ForUnitType(reg.unitType, reg.currentSize);
                    foreach (var kvp in needs.requirements)
                        demand[kvp.Key] += kvp.Value;
                }
            }
            return demand;
        }

        /// <summary>Calculate combat effectiveness modifier based on equipment fulfillment (0.3 to 1.0)</summary>
        public static float GetCombatModifier(FactionData faction, ArmyData army)
        {
            if (faction.equipment == null) return 1f;

            float totalNeeded = 0f;
            float totalFulfilled = 0f;

            foreach (var reg in army.regiments)
            {
                var needs = RegimentEquipmentNeeds.ForUnitType(reg.unitType, reg.currentSize);
                foreach (var kvp in needs.requirements)
                {
                    totalNeeded += kvp.Value;
                    totalFulfilled += Mathf.Min(kvp.Value, faction.equipment.Get(kvp.Key));
                }
            }

            if (totalNeeded <= 0f) return 1f;
            float ratio = totalFulfilled / totalNeeded;
            return Mathf.Lerp(0.30f, 1.0f, ratio); // -70% at 0 equipment, 100% at full
        }

        /// <summary>Process per-turn equipment attrition for all armies of a faction</summary>
        public static void ProcessAttrition(FactionData faction, Dictionary<string, ArmyData> armies)
        {
            if (faction.equipment == null) return;

            foreach (string armyId in faction.armyIds)
            {
                if (!armies.ContainsKey(armyId)) continue;
                var army = armies[armyId];
                foreach (var reg in army.regiments)
                {
                    var attrition = RegimentEquipmentNeeds.AttritionPerTurn(reg.unitType, reg.currentSize);
                    foreach (var kvp in attrition)
                        faction.equipment.Consume(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>Get conscription manpower multiplier</summary>
        public static float GetConscriptionPercent(ConscriptionLaw law) => law switch
        {
            ConscriptionLaw.Volunteer => 0.01f,
            ConscriptionLaw.LimitedConscription => 0.025f,
            ConscriptionLaw.ExtendedConscription => 0.05f,
            ConscriptionLaw.ServiceByRequirement => 0.08f,
            ConscriptionLaw.TotalMobilization => 0.15f,
            _ => 0.01f
        };

        /// <summary>Get stability penalty from conscription law</summary>
        public static float GetConscriptionStabilityPenalty(ConscriptionLaw law) => law switch
        {
            ConscriptionLaw.Volunteer => 0f,
            ConscriptionLaw.LimitedConscription => -0.02f,
            ConscriptionLaw.ExtendedConscription => -0.05f,
            ConscriptionLaw.ServiceByRequirement => -0.10f,
            ConscriptionLaw.TotalMobilization => -0.20f,
            _ => 0f
        };

        /// <summary>Get factory output modifier from economy law</summary>
        public static float GetEconomyModifier(EconomyLaw law) => law switch
        {
            EconomyLaw.CivilianEconomy => 1.0f,
            EconomyLaw.PreMobilization => 1.10f,
            EconomyLaw.WarEconomy => 1.25f,
            EconomyLaw.TotalWar => 1.50f,
            _ => 1.0f
        };

        /// <summary>Get stability penalty from economy law</summary>
        public static float GetEconomyStabilityPenalty(EconomyLaw law) => law switch
        {
            EconomyLaw.CivilianEconomy => 0f,
            EconomyLaw.PreMobilization => -0.05f,
            EconomyLaw.WarEconomy => -0.10f,
            EconomyLaw.TotalWar => -0.20f,
            _ => 0f
        };

        /// <summary>Give a faction starting equipment based on their armies</summary>
        public static void InitializeStartingEquipment(FactionData faction, Dictionary<string, ArmyData> armies)
        {
            if (faction.equipment == null) faction.equipment = new EquipmentStockpile();
            
            // Give 150% of current demand as starting stockpile
            var demand = CalculateTotalDemand(faction, armies);
            foreach (var kvp in demand)
                faction.equipment.Add(kvp.Key, kvp.Value * 1.5f);
        }
    }
}
