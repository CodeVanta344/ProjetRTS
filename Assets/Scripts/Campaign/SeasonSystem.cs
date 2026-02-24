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
    /// Campaign season/date system. Tracks day, month, season, year.
    /// Each season = 90 days = 3 months of 30 days each.
    /// </summary>
    public static class SeasonSystem
    {
        // Current state
        private static int currentYear = 1700;
        private static Season currentSeason = Season.Spring;
        private static int dayInSeason = 0;  // 0..89

        public static int CurrentYear => currentYear;
        public static Season CurrentSeason => currentSeason;
        public static int DayInSeason => dayInSeason;
        
        /// <summary>Current day of the month (1-30).</summary>
        public static int DayOfMonth => (dayInSeason % 30) + 1;
        
        /// <summary>Month index within the year (1-12).</summary>
        public static int CurrentMonth
        {
            get
            {
                int seasonBase = currentSeason switch
                {
                    Season.Spring => 3,  // Apr, May, Jun
                    Season.Summer => 6,  // Jul, Aug, Sep
                    Season.Autumn => 9,  // Oct, Nov, Dec
                    Season.Winter => 0,  // Jan, Feb, Mar
                    _ => 0
                };
                int monthInSeason = dayInSeason / 30; // 0, 1, 2
                return seasonBase + monthInSeason + 1; // 1-12
            }
        }

        public static string CurrentSeasonName => currentSeason switch
        {
            Season.Spring => "Printemps",
            Season.Summer => "Été",
            Season.Autumn => "Automne",
            Season.Winter => "Hiver",
            _ => "?"
        };

        private static readonly string[] MonthNames = {
            "Jan", "Fév", "Mar", "Avr", "Mai", "Jun",
            "Jul", "Aoû", "Sep", "Oct", "Nov", "Déc"
        };
        
        /// <summary>Get current month name.</summary>
        public static string GetMonthName() => MonthNames[Mathf.Clamp(CurrentMonth - 1, 0, 11)];
        
        /// <summary>Full date string: "12 Avr 1700"</summary>
        public static string DateString => $"{DayOfMonth} {GetMonthName()} {currentYear}";
        
        /// <summary>Short date: "Avr 1700"</summary>
        public static string ShortDateString => $"{GetMonthName()} {currentYear}";

        /// <summary>Initialize season system at game start</summary>
        public static void Initialize(int startYear = 1700)
        {
            currentYear = startYear;
            currentSeason = Season.Spring;
            dayInSeason = 0;
        }

        /// <summary>Advance by one day. Returns true if a new season started.</summary>
        public static bool AdvanceDay()
        {
            dayInSeason++;
            if (dayInSeason >= 90)
            {
                dayInSeason = 0;
                AdvanceSeason();
                return true;
            }
            return false;
        }

        /// <summary>Advance to next season (called automatically by AdvanceDay).</summary>
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

            if (currentSeason == Season.Winter)
            {
                if (terrain == ProvinceTerrainType.Snow) baseMod *= 0.5f;
                if (terrain == ProvinceTerrainType.Mountains) baseMod *= 0.6f;
                if (terrain == ProvinceTerrainType.Marsh) baseMod *= 0.4f;
            }

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

            if (currentSeason == Season.Winter && isRussianTerritory)
                baseMod *= 1.5f;

            if (currentSeason == Season.Winter && terrain == ProvinceTerrainType.Snow)
                baseMod *= 1.5f;

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
                Season.Spring => 1.2f,
                Season.Summer => 1.0f,
                Season.Autumn => 1.8f,
                Season.Winter => 0.3f,
                _ => 1.0f
            };
        }

        /// <summary>Gold/production modifier for current season</summary>
        public static float GetProductionModifier()
        {
            return currentSeason switch
            {
                Season.Spring => 1.0f,
                Season.Summer => 1.15f,
                Season.Autumn => 1.0f,
                Season.Winter => 0.75f,
                _ => 1.0f
            };
        }

        /// <summary>Population growth modifier for current season</summary>
        public static float GetPopulationGrowthModifier()
        {
            return currentSeason switch
            {
                Season.Spring => 1.3f,
                Season.Summer => 1.0f,
                Season.Autumn => 0.9f,
                Season.Winter => 0.5f,
                _ => 1.0f
            };
        }

        /// <summary>Can sieges be conducted this season?</summary>
        public static bool CanSiege(ProvinceTerrainType terrain)
        {
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
                Season.Spring => 1.2f,
                Season.Summer => 1.0f,
                Season.Autumn => 0.8f,
                Season.Winter => 0.6f,
                _ => 1.0f
            };
        }

        /// <summary>Get the season for battle terrain</summary>
        public static string GetBattleSeasonString()
        {
            return currentSeason switch
            {
                Season.Spring => "summer",
                Season.Summer => "summer",
                Season.Autumn => "autumn",
                Season.Winter => "winter",
                _ => "summer"
            };
        }

        /// <summary>Is this province in an extended winter zone? (Russia, Scandinavia)</summary>
        public static bool IsExtendedWinterZone(string provinceId)
        {
            string[] extendedWinter = {
                "moscow", "st_petersburg", "novgorod", "pskov", "tver", "smolensk",
                "vologda", "kostroma", "yaroslavl", "perm", "vyatka", "ufa",
                "orenburg", "arkhangelsk", "olonet", "karelia", "finland",
                "sweden_north", "norway"
            };
            return System.Array.IndexOf(extendedWinter, provinceId) >= 0;
        }

        /// <summary>Get effective season for a province</summary>
        public static Season GetEffectiveSeason(string provinceId)
        {
            if (currentSeason == Season.Autumn && IsExtendedWinterZone(provinceId))
                return Season.Winter;
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
