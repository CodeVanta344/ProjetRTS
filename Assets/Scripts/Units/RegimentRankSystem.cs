using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Regiment rank progression system. Regiments gain XP through battles,
    /// advance through 10 ranks, and can be promoted to elite unit types at key thresholds.
    /// </summary>
    public enum RegimentRank
    {
        Conscrit = 0,       // Fresh recruit, never seen combat
        Fusilier = 1,       // Basic training complete
        Soldat = 2,         // First battle survived
        Caporal = 3,        // Small squad leader quality
        Sergent = 4,        // Reliable, seasoned
        Veteran = 5,        // Battle-hardened
        Elite = 6,          // Promotion threshold #1
        Confirmé = 7,       // Proven elite
        Garde = 8,          // Promotion threshold #2
        VielleGarde = 9     // Legendary — best of the best
    }

    /// <summary>
    /// Stat multipliers applied per rank on top of base UnitData values.
    /// </summary>
    [System.Serializable]
    public struct RankStatModifiers
    {
        public float hpMultiplier;
        public float damageMultiplier;
        public float accuracyBonus;       // Flat addition (0.0 - 0.15)
        public float moraleMaxBonus;      // Flat addition
        public float moraleRecoveryMult;
        public float fleeThresholdReduction; // Subtracted from flee threshold
        public float meleeDamageMultiplier;
        public float speedMultiplier;

        public static RankStatModifiers Identity => new RankStatModifiers
        {
            hpMultiplier = 1f,
            damageMultiplier = 1f,
            accuracyBonus = 0f,
            moraleMaxBonus = 0f,
            moraleRecoveryMult = 1f,
            fleeThresholdReduction = 0f,
            meleeDamageMultiplier = 1f,
            speedMultiplier = 1f
        };
    }

    public static class RegimentRankSystem
    {
        // ==================== XP THRESHOLDS ====================
        // XP is 0-100 float, accumulated across battles
        public static readonly float[] XP_THRESHOLDS = new float[]
        {
            0f,     // Rank 0: Conscrit
            5f,     // Rank 1: Fusilier
            12f,    // Rank 2: Soldat
            22f,    // Rank 3: Caporal
            35f,    // Rank 4: Sergent
            50f,    // Rank 5: Vétéran
            60f,    // Rank 6: Elite (promotion #1)
            72f,    // Rank 7: Confirmé
            85f,    // Rank 8: Garde (promotion #2)
            95f     // Rank 9: Vieille Garde
        };

        // ==================== RANK FROM XP ====================
        public static RegimentRank GetRank(float experience)
        {
            for (int i = XP_THRESHOLDS.Length - 1; i >= 0; i--)
            {
                if (experience >= XP_THRESHOLDS[i])
                    return (RegimentRank)i;
            }
            return RegimentRank.Conscrit;
        }

        public static int GetRankIndex(float experience)
        {
            return (int)GetRank(experience);
        }

        /// <summary>XP needed to reach the next rank, or -1 if max rank.</summary>
        public static float GetXPToNextRank(float experience)
        {
            int current = GetRankIndex(experience);
            if (current >= XP_THRESHOLDS.Length - 1) return -1f;
            return XP_THRESHOLDS[current + 1] - experience;
        }

        /// <summary>Progress fraction (0-1) within the current rank tier.</summary>
        public static float GetRankProgress(float experience)
        {
            int current = GetRankIndex(experience);
            if (current >= XP_THRESHOLDS.Length - 1) return 1f;
            float floor = XP_THRESHOLDS[current];
            float ceiling = XP_THRESHOLDS[current + 1];
            return Mathf.Clamp01((experience - floor) / (ceiling - floor));
        }

        // ==================== RANK NAMES ====================
        /// <summary>Generic rank name from rank index (no unit-type-specific variant).</summary>
        public static string GetRankName(int rankIndex)
        {
            RegimentRank rank = (RegimentRank)Mathf.Clamp(rankIndex, 0, TotalRanks - 1);
            return GetRankName(rank, UnitType.LineInfantry);
        }

        public static string GetRankName(RegimentRank rank, UnitType unitType)
        {
            bool isCav = unitType == UnitType.Cavalry || unitType == UnitType.Hussar ||
                         unitType == UnitType.Lancer || unitType == UnitType.GuardCavalry;
            bool isArt = unitType == UnitType.Artillery || unitType == UnitType.GuardArtillery;

            return rank switch
            {
                RegimentRank.Conscrit => "Conscrit",
                RegimentRank.Fusilier => isCav ? "Cavalier" : isArt ? "Canonnier" : "Fusilier",
                RegimentRank.Soldat => isCav ? "Brigadier" : isArt ? "Artilleur" : "Soldat",
                RegimentRank.Caporal => isCav ? "Maréchal des logis" : isArt ? "Caporal d'artillerie" : "Caporal",
                RegimentRank.Sergent => isCav ? "Sous-officier" : isArt ? "Sergent d'artillerie" : "Sergent",
                RegimentRank.Veteran => "Vétéran",
                RegimentRank.Elite => isCav ? "Chasseur à cheval" : isArt ? "Artilleur d'élite" : "Grenadier",
                RegimentRank.Confirmé => isCav ? "Cuirassier" : isArt ? "Canonnier de la Garde" : "Grenadier confirmé",
                RegimentRank.Garde => isCav ? "Garde à cheval" : isArt ? "Artilleur de la Garde" : "Garde",
                RegimentRank.VielleGarde => isCav ? "Garde d'honneur" : isArt ? "Vieille Garde (Art.)" : "Vieille Garde",
                _ => "Conscrit"
            };
        }

        /// <summary>Short rank abbreviation for HUD display.</summary>
        public static string GetRankAbbreviation(RegimentRank rank)
        {
            return rank switch
            {
                RegimentRank.Conscrit => "Csc",
                RegimentRank.Fusilier => "Fus",
                RegimentRank.Soldat => "Sol",
                RegimentRank.Caporal => "Cpl",
                RegimentRank.Sergent => "Sgt",
                RegimentRank.Veteran => "Vét",
                RegimentRank.Elite => "Élt",
                RegimentRank.Confirmé => "Cnf",
                RegimentRank.Garde => "Grd",
                RegimentRank.VielleGarde => "VG",
                _ => "?"
            };
        }

        /// <summary>Color for rank display in UI.</summary>
        public static Color GetRankColor(RegimentRank rank)
        {
            return rank switch
            {
                RegimentRank.Conscrit => new Color(0.6f, 0.6f, 0.6f),          // Grey
                RegimentRank.Fusilier => new Color(0.7f, 0.7f, 0.7f),          // Light grey
                RegimentRank.Soldat => Color.white,                              // White
                RegimentRank.Caporal => new Color(0.5f, 0.8f, 0.5f),           // Green
                RegimentRank.Sergent => new Color(0.3f, 0.9f, 0.3f),           // Bright green
                RegimentRank.Veteran => new Color(0.4f, 0.7f, 1f),             // Blue
                RegimentRank.Elite => new Color(0.3f, 0.5f, 1f),               // Deep blue
                RegimentRank.Confirmé => new Color(0.8f, 0.6f, 1f),            // Purple
                RegimentRank.Garde => new Color(1f, 0.85f, 0.3f),              // Gold
                RegimentRank.VielleGarde => new Color(1f, 0.65f, 0.1f),        // Bright gold
                _ => Color.grey
            };
        }

        // ==================== STAT MULTIPLIERS ====================
        public static RankStatModifiers GetStatModifiers(RegimentRank rank)
        {
            int r = (int)rank;
            return new RankStatModifiers
            {
                hpMultiplier = 1f + r * 0.03f,
                damageMultiplier = 1f + r * 0.04f,
                accuracyBonus = r * 0.015f,
                moraleMaxBonus = r * 3f,
                moraleRecoveryMult = 1f + r * 0.05f,
                fleeThresholdReduction = r * 1.5f,
                meleeDamageMultiplier = 1f + r * 0.04f,
                speedMultiplier = 1f + r * 0.01f
            };
        }

        // ==================== PROMOTION CHAINS ====================

        /// <summary>
        /// Check if a unit type can be promoted at the given rank.
        /// Returns the new UnitType, or null if no promotion available.
        /// </summary>
        public static UnitType? GetPromotionType(UnitType currentType, RegimentRank rank)
        {
            if (rank == RegimentRank.Elite) // Rank 6 — first promotion
            {
                return currentType switch
                {
                    UnitType.LineInfantry => UnitType.Grenadier,
                    UnitType.LightInfantry => UnitType.Grenadier,
                    UnitType.Cavalry => UnitType.Hussar,
                    UnitType.Lancer => UnitType.Hussar,
                    _ => null
                };
            }

            if (rank == RegimentRank.Garde) // Rank 8 — second promotion
            {
                return currentType switch
                {
                    UnitType.Grenadier => UnitType.GuardInfantry,
                    UnitType.Hussar => UnitType.GuardCavalry,
                    UnitType.Artillery => UnitType.GuardArtillery,
                    _ => null
                };
            }

            return null;
        }

        /// <summary>Check if any promotion is available at the current rank for this type.</summary>
        public static bool HasPromotionAvailable(UnitType currentType, RegimentRank rank)
        {
            return GetPromotionType(currentType, rank) != null;
        }

        // ==================== BATTLE XP CALCULATION ====================

        /// <summary>
        /// Calculate XP gained from a single battle.
        /// </summary>
        /// <param name="won">Whether the regiment's side won</param>
        /// <param name="initialSize">Regiment size before battle</param>
        /// <param name="casualties">Soldiers lost in battle</param>
        /// <param name="kills">Enemy soldiers killed by this regiment</param>
        /// <returns>XP gained (0-15 per battle)</returns>
        public static float CalculateBattleXP(bool won, int initialSize, int casualties, int kills)
        {
            float xp = 5f; // Base participation XP

            // Win bonus
            if (won) xp += 3f;

            // Kill bonus (scaled to regiment size)
            if (initialSize > 0)
            {
                float killRatio = (float)kills / initialSize;
                xp += Mathf.Min(killRatio * 8f, 5f); // Up to +5 for high kill ratio
            }

            // Heavy casualties reduce XP gain slightly (survivors learn, but disrupted)
            if (initialSize > 0)
            {
                float casualtyRatio = (float)casualties / initialSize;
                if (casualtyRatio > 0.5f)
                    xp -= 1f; // Severe losses
            }

            // Clamp
            return Mathf.Clamp(xp, 1f, 15f);
        }

        // ==================== DISPLAY HELPERS ====================

        /// <summary>Get a formatted string like "Rang 3 — Caporal (XP: 25/35)"</summary>
        public static string GetRankDisplayString(float experience, UnitType unitType)
        {
            RegimentRank rank = GetRank(experience);
            string name = GetRankName(rank, unitType);
            int rankIdx = (int)rank;

            if (rankIdx >= XP_THRESHOLDS.Length - 1)
                return $"Rang {rankIdx} — {name} (MAX)";

            float nextXP = XP_THRESHOLDS[rankIdx + 1];
            return $"Rang {rankIdx} — {name} (XP: {experience:F0}/{nextXP:F0})";
        }

        /// <summary>Get total number of ranks.</summary>
        public static int TotalRanks => XP_THRESHOLDS.Length;
    }
}
