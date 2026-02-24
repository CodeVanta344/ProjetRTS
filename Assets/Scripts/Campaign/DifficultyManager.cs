using UnityEngine;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Centralized difficulty system. All game systems query this for multipliers.
    /// Co-op mode uses elevated difficulty by default to ensure each role is stressful.
    /// </summary>
    public class DifficultyManager
    {
        private static DifficultyManager _instance;
        public static DifficultyManager Instance => _instance ??= new DifficultyManager();

        public DifficultyPreset CurrentPreset { get; private set; } = DifficultyPreset.Normal;

        // ==================== ECONOMY (stress for Intendant) ====================
        public float ResourceScarcityMultiplier { get; private set; } = 1.0f;
        public float ConstructionTimeMultiplier { get; private set; } = 1.0f;
        public float FactoryOutputMultiplier { get; private set; } = 1.0f;
        public float MaintenanceCostMultiplier { get; private set; } = 1.0f;
        public float EquipmentAttritionMultiplier { get; private set; } = 1.0f;
        public float InflationRate { get; private set; } = 0f;

        // ==================== MILITARY (stress for Marshal) ====================
        public float EnemyAIAggression { get; private set; } = 1.0f;
        public float CombatDamageMultiplier { get; private set; } = 1.0f;
        public float SupplyPenaltyMultiplier { get; private set; } = 1.0f;
        public float ReinforcementRateMultiplier { get; private set; } = 1.0f;
        public float OrganizationRecoveryMultiplier { get; private set; } = 1.0f;
        public float ArmyFatigueRate { get; private set; } = 0f;
        public float WinterAttritionMultiplier { get; private set; } = 1.0f;
        public int SiegeBaseTurns { get; private set; } = 3;

        // ==================== POLITICAL (stress for Chancellor) ====================
        public float StabilityDecayRate { get; private set; } = 1.0f;
        public float PoliticalPowerGainMultiplier { get; private set; } = 1.0f;
        public float ResearchTimeMultiplier { get; private set; } = 1.0f;
        public float DiplomaticScoreMultiplier { get; private set; } = 1.0f;
        public float WarSupportDecayRate { get; private set; } = 1.0f;
        public float LawChangeCostMultiplier { get; private set; } = 1.0f;
        public float CoalitionRiskMultiplier { get; private set; } = 1.0f;
        public int LawChangeCooldownTurns { get; private set; } = 3;

        // ==================== EVENTS ====================
        public float CrisisEventChance { get; private set; } = 0.05f;
        public float CrisisEventSeverity { get; private set; } = 1.0f;

        // ============================================================
        // PRESETS
        // ============================================================

        public void SetPreset(DifficultyPreset preset)
        {
            CurrentPreset = preset;
            switch (preset)
            {
                case DifficultyPreset.Easy:
                    ApplyEasy();
                    break;
                case DifficultyPreset.Normal:
                    ApplyNormal();
                    break;
                case DifficultyPreset.Hard:
                    ApplyHard();
                    break;
                case DifficultyPreset.CoopDefault:
                    ApplyCoopDefault();
                    break;
            }

            Debug.Log($"[DifficultyManager] Preset set to: {preset}");
        }

        private void ApplyEasy()
        {
            ResourceScarcityMultiplier = 1.2f;
            ConstructionTimeMultiplier = 0.8f;
            FactoryOutputMultiplier = 1.2f;
            MaintenanceCostMultiplier = 0.8f;
            EquipmentAttritionMultiplier = 0.7f;
            InflationRate = 0f;

            EnemyAIAggression = 0.7f;
            CombatDamageMultiplier = 0.8f;
            SupplyPenaltyMultiplier = 0.7f;
            ReinforcementRateMultiplier = 1.3f;
            OrganizationRecoveryMultiplier = 1.3f;
            ArmyFatigueRate = 0f;
            WinterAttritionMultiplier = 0.5f;
            SiegeBaseTurns = 2;

            StabilityDecayRate = 0.7f;
            PoliticalPowerGainMultiplier = 1.3f;
            ResearchTimeMultiplier = 0.8f;
            DiplomaticScoreMultiplier = 1.2f;
            WarSupportDecayRate = 0.7f;
            LawChangeCostMultiplier = 0.7f;
            CoalitionRiskMultiplier = 0.5f;
            LawChangeCooldownTurns = 2;

            CrisisEventChance = 0.02f;
            CrisisEventSeverity = 0.6f;
        }

        private void ApplyNormal()
        {
            ResourceScarcityMultiplier = 1.0f;
            ConstructionTimeMultiplier = 1.0f;
            FactoryOutputMultiplier = 1.0f;
            MaintenanceCostMultiplier = 1.0f;
            EquipmentAttritionMultiplier = 1.0f;
            InflationRate = 0f;

            EnemyAIAggression = 1.0f;
            CombatDamageMultiplier = 1.0f;
            SupplyPenaltyMultiplier = 1.0f;
            ReinforcementRateMultiplier = 1.0f;
            OrganizationRecoveryMultiplier = 1.0f;
            ArmyFatigueRate = 0f;
            WinterAttritionMultiplier = 1.0f;
            SiegeBaseTurns = 3;

            StabilityDecayRate = 1.0f;
            PoliticalPowerGainMultiplier = 1.0f;
            ResearchTimeMultiplier = 1.0f;
            DiplomaticScoreMultiplier = 1.0f;
            WarSupportDecayRate = 1.0f;
            LawChangeCostMultiplier = 1.0f;
            CoalitionRiskMultiplier = 1.0f;
            LawChangeCooldownTurns = 3;

            CrisisEventChance = 0.05f;
            CrisisEventSeverity = 1.0f;
        }

        private void ApplyHard()
        {
            ResourceScarcityMultiplier = 0.7f;
            ConstructionTimeMultiplier = 1.3f;
            FactoryOutputMultiplier = 0.8f;
            MaintenanceCostMultiplier = 1.3f;
            EquipmentAttritionMultiplier = 1.3f;
            InflationRate = 0.03f;

            EnemyAIAggression = 1.4f;
            CombatDamageMultiplier = 1.2f;
            SupplyPenaltyMultiplier = 1.3f;
            ReinforcementRateMultiplier = 0.7f;
            OrganizationRecoveryMultiplier = 0.7f;
            ArmyFatigueRate = 0.02f;
            WinterAttritionMultiplier = 1.5f;
            SiegeBaseTurns = 5;

            StabilityDecayRate = 1.3f;
            PoliticalPowerGainMultiplier = 0.8f;
            ResearchTimeMultiplier = 1.2f;
            DiplomaticScoreMultiplier = 0.8f;
            WarSupportDecayRate = 1.3f;
            LawChangeCostMultiplier = 1.2f;
            CoalitionRiskMultiplier = 1.4f;
            LawChangeCooldownTurns = 5;

            CrisisEventChance = 0.08f;
            CrisisEventSeverity = 1.3f;
        }

        /// <summary>
        /// Co-op default: higher difficulty to ensure all roles are stressed and coordination matters.
        /// Tougher than Hard in most areas.
        /// </summary>
        private void ApplyCoopDefault()
        {
            ResourceScarcityMultiplier = 0.6f;
            ConstructionTimeMultiplier = 1.5f;
            FactoryOutputMultiplier = 0.75f;
            MaintenanceCostMultiplier = 1.4f;
            EquipmentAttritionMultiplier = 1.3f;
            InflationRate = 0.05f;

            EnemyAIAggression = 1.5f;
            CombatDamageMultiplier = 1.25f;
            SupplyPenaltyMultiplier = 1.5f;
            ReinforcementRateMultiplier = 0.6f;
            OrganizationRecoveryMultiplier = 0.7f;
            ArmyFatigueRate = 0.03f;
            WinterAttritionMultiplier = 2.0f;
            SiegeBaseTurns = 5;

            StabilityDecayRate = 1.5f;
            PoliticalPowerGainMultiplier = 0.7f;
            ResearchTimeMultiplier = 1.3f;
            DiplomaticScoreMultiplier = 0.8f;
            WarSupportDecayRate = 1.5f;
            LawChangeCostMultiplier = 1.3f;
            CoalitionRiskMultiplier = 1.5f;
            LawChangeCooldownTurns = 5;

            CrisisEventChance = 0.10f;
            CrisisEventSeverity = 1.5f;
        }

        // ============================================================
        // SEASON HELPERS
        // ============================================================

        /// <summary>Check if a given turn is a winter turn (Oct-Mar)</summary>
        public static bool IsWinterTurn(int turn)
        {
            int month = (turn % 12); // 0-indexed month
            return month >= 9 || month <= 2; // Oct, Nov, Dec, Jan, Feb, Mar
        }

        /// <summary>Get season name for display</summary>
        public static string GetSeason(int turn)
        {
            int month = (turn % 12);
            if (month >= 2 && month <= 4) return "Printemps";
            if (month >= 5 && month <= 7) return "Été";
            if (month >= 8 && month <= 10) return "Automne";
            return "Hiver";
        }
    }

    public enum DifficultyPreset
    {
        Easy,
        Normal,
        Hard,
        CoopDefault
    }
}
