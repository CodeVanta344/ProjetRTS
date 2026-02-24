using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    public enum Season
    {
        Spring,   // Printemps — bonus food, normal movement
        Summer,   // Été — bonus production, normal movement
        Autumn,   // Automne — harvest (big food), movement slows
        Winter    // Hiver — attrition×2, movement÷2, supply demand×1.5
    }

    /// <summary>
    /// Campaign season system. 4 turns = 1 year. Each season has distinct effects
    /// on movement, attrition, supply, production, and food.
    /// </summary>
    public static class SeasonSystem
    {
        // Current state
        private static int currentYear = 1700;
        private static Season currentSeason = Season.Spring;

        public static int CurrentYear => currentYear;
        public static Season CurrentSeason => currentSeason;
        public static string CurrentSeasonName => currentSeason switch
        {
            Season.Spring => "Printemps",
            Season.Summer => "Été",
            Season.Autumn => "Automne",
            Season.Winter => "Hiver",
            _ => "?"
        };
        public static string DateString => $"{CurrentSeasonName} {currentYear}";

        /// <summary>Initialize season system at game start</summary>
        public static void Initialize(int startYear = 1700)
        {
            currentYear = startYear;
            currentSeason = Season.Spring;
        }

        /// <summary>Advance to next season. Call once per turn.</summary>
        public static void AdvanceSeason()
        {
            switch (currentSeason)
            {
                case Season.Spring: currentSeason = Season.Summer; break;
                case Season.Summer: currentSeason = Season.Autumn; break;
                case Season.Autumn: currentSeason = Season.Winter; break;
                case Season.Winter:
                    currentSeason = Season.Spring;
                    currentYear++;
                    break;
            }
        }

        // ======================== MODIFIERS ========================

        /// <summary>Movement speed multiplier for current season</summary>
        public static float GetMovementModifier(ProvinceTerrainType terrain)
        {
            float baseMod = currentSeason switch
            {
                Season.Spring => 1.0f,
                Season.Summer => 1.0f,
                Season.Autumn => 0.85f,
                Season.Winter => 0.50f,
                _ => 1.0f
            };

            // Snow/mountain provinces are even worse in winter
            if (currentSeason == Season.Winter)
            {
                if (terrain == ProvinceTerrainType.Snow) baseMod *= 0.5f;
                if (terrain == ProvinceTerrainType.Mountains) baseMod *= 0.6f;
                if (terrain == ProvinceTerrainType.Marsh) baseMod *= 0.4f;
            }

            // Desert is bad in summer
            if (currentSeason == Season.Summer && terrain == ProvinceTerrainType.Desert)
                baseMod *= 0.7f;

            return baseMod;
        }

        /// <summary>Attrition multiplier for current season</summary>
        public static float GetAttritionModifier(ProvinceTerrainType terrain, bool isRussianTerritory)
        {
            float baseMod = currentSeason switch
            {
                Season.Spring => 1.0f,
                Season.Summer => 0.8f,
                Season.Autumn => 1.2f,
                Season.Winter => 2.0f,
                _ => 1.0f
            };

            // Russian winter is legendary
            if (currentSeason == Season.Winter && isRussianTerritory)
                baseMod *= 1.5f; // 3x total in Russian winter

            // Snow provinces double winter attrition
            if (currentSeason == Season.Winter && terrain == ProvinceTerrainType.Snow)
                baseMod *= 1.5f;

            // Desert summer attrition
            if (currentSeason == Season.Summer && terrain == ProvinceTerrainType.Desert)
                baseMod *= 1.8f;

            return baseMod;
        }

        /// <summary>Supply demand multiplier for current season</summary>
        public static float GetSupplyDemandModifier()
        {
            return currentSeason switch
            {
                Season.Spring => 1.0f,
                Season.Summer => 0.9f,
                Season.Autumn => 1.0f,
                Season.Winter => 1.5f,
                _ => 1.0f
            };
        }

        /// <summary>Food production modifier for current season</summary>
        public static float GetFoodProductionModifier()
        {
            return currentSeason switch
            {
                Season.Spring => 1.2f,    // Planting season bonus
                Season.Summer => 1.0f,
                Season.Autumn => 1.8f,    // Harvest!
                Season.Winter => 0.3f,    // Almost no food production
                _ => 1.0f
            };
        }

        /// <summary>Gold/production modifier for current season</summary>
        public static float GetProductionModifier()
        {
            return currentSeason switch
            {
                Season.Spring => 1.0f,
                Season.Summer => 1.15f,   // Long days, good weather
                Season.Autumn => 1.0f,
                Season.Winter => 0.75f,   // Reduced activity
                _ => 1.0f
            };
        }

        /// <summary>Population growth modifier for current season</summary>
        public static float GetPopulationGrowthModifier()
        {
            return currentSeason switch
            {
                Season.Spring => 1.3f,    // Birth season
                Season.Summer => 1.0f,
                Season.Autumn => 0.9f,
                Season.Winter => 0.5f,    // Disease, cold
                _ => 1.0f
            };
        }

        /// <summary>Can sieges be conducted this season?</summary>
        public static bool CanSiege(ProvinceTerrainType terrain)
        {
            // No sieges in deep winter except in mild climates
            if (currentSeason == Season.Winter)
            {
                return terrain == ProvinceTerrainType.Coastal ||
                       terrain == ProvinceTerrainType.Urban ||
                       terrain == ProvinceTerrainType.Desert;
            }
            return true;
        }

        /// <summary>Recruitment speed modifier for current season</summary>
        public static float GetRecruitmentModifier()
        {
            return currentSeason switch
            {
                Season.Spring => 1.2f,    // Men available after winter
                Season.Summer => 1.0f,
                Season.Autumn => 0.8f,    // Harvest needs workers
                Season.Winter => 0.6f,    // Hard to recruit in winter
                _ => 1.0f
            };
        }

        /// <summary>Get the season for battle terrain (used by BattleTerrainConfigurator)</summary>
        public static string GetBattleSeasonString()
        {
            return currentSeason switch
            {
                Season.Spring => "summer",  // Spring uses summer visuals
                Season.Summer => "summer",
                Season.Autumn => "autumn",
                Season.Winter => "winter",
                _ => "summer"
            };
        }

        /// <summary>Is this province in an extended winter zone? (Russia, Scandinavia)</summary>
        public static bool IsExtendedWinterZone(string provinceId)
        {
            // Russian provinces have extended winter (Autumn also counts as harsh)
            string[] extendedWinter = {
                "moscow", "st_petersburg", "novgorod", "pskov", "tver", "smolensk",
                "vologda", "kostroma", "yaroslavl", "perm", "vyatka", "ufa",
                "orenburg", "arkhangelsk", "olonet", "karelia", "finland",
                "sweden_north", "norway"
            };
            return System.Array.IndexOf(extendedWinter, provinceId) >= 0;
        }

        /// <summary>Get effective season for a province (extended winter zones feel winter in autumn too)</summary>
        public static Season GetEffectiveSeason(string provinceId)
        {
            if (currentSeason == Season.Autumn && IsExtendedWinterZone(provinceId))
                return Season.Winter; // Early winter in northern regions
            return currentSeason;
        }

        /// <summary>Get season icon for UI</summary>
        public static string GetSeasonIcon() => currentSeason switch
        {
            Season.Spring => "🌱",
            Season.Summer => "☀️",
            Season.Autumn => "🍂",
            Season.Winter => "❄️",
            _ => "?"
        };

        /// <summary>Get season color for UI</summary>
        public static Color GetSeasonColor() => currentSeason switch
        {
            Season.Spring => new Color(0.4f, 0.8f, 0.3f),
            Season.Summer => new Color(1.0f, 0.85f, 0.2f),
            Season.Autumn => new Color(0.9f, 0.5f, 0.2f),
            Season.Winter => new Color(0.6f, 0.8f, 1.0f),
            _ => Color.white
        };
    }
}
