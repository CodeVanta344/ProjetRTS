using UnityEngine;

namespace NapoleonicWars.Core
{
    public enum DifficultyLevel
    {
        Recruit,   // Very easy — AI barely reacts
        Easy,
        Normal,
        Hard,
        Legendary  // Brutal — AI uses every trick
    }

    /// <summary>
    /// Global difficulty settings affecting AI behavior, player/enemy stat multipliers,
    /// and battle conditions. Persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public class DifficultySettings : MonoBehaviour
    {
        public static DifficultySettings Instance { get; private set; }

        [SerializeField] private DifficultyLevel currentDifficulty = DifficultyLevel.Normal;
        public DifficultyLevel CurrentDifficulty => currentDifficulty;

        // Player multipliers
        public float PlayerDamageMultiplier { get; private set; } = 1f;
        public float PlayerMoraleMultiplier { get; private set; } = 1f;
        public float PlayerAccuracyBonus { get; private set; } = 0f;

        // Enemy multipliers
        public float EnemyDamageMultiplier { get; private set; } = 1f;
        public float EnemyMoraleMultiplier { get; private set; } = 1f;
        public float EnemyAccuracyBonus { get; private set; } = 0f;
        public float EnemyReactionSpeed { get; private set; } = 1f;

        // AI behavior flags
        public float AIAggressiveness { get; private set; } = 0.5f;
        public bool AIUsesReserves { get; private set; } = true;
        public bool AIFlankingEnabled { get; private set; } = true;
        public bool AICoordinatesArms { get; private set; } = false;
        public bool AIUsesPhasePlanning { get; private set; } = false;
        public bool AICounterFlanks { get; private set; } = false;
        public bool AIFeintRetreat { get; private set; } = false;
        public bool AIPursuesRouting { get; private set; } = false;
        public bool AIFocusFire { get; private set; } = false;
        public float AIDecisionInterval { get; private set; } = 3f;

        // Economy (campaign)
        public float PlayerIncomeMultiplier { get; private set; } = 1f;
        public float EnemyIncomeMultiplier { get; private set; } = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyDifficulty(currentDifficulty);
        }

        public void SetDifficulty(DifficultyLevel level)
        {
            currentDifficulty = level;
            ApplyDifficulty(level);
            PlayerPrefs.SetInt("Difficulty", (int)level);
            PlayerPrefs.Save();
            Debug.Log($"[Difficulty] Set to {level}");
        }

        public void LoadSavedDifficulty()
        {
            int saved = PlayerPrefs.GetInt("Difficulty", 2); // Default = Normal (index 2)
            SetDifficulty((DifficultyLevel)Mathf.Clamp(saved, 0, 4));
        }

        private void ApplyDifficulty(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Recruit:
                    PlayerDamageMultiplier = 1.5f;
                    PlayerMoraleMultiplier = 1.5f;
                    PlayerAccuracyBonus = 0.15f;
                    EnemyDamageMultiplier = 0.5f;
                    EnemyMoraleMultiplier = 0.5f;
                    EnemyAccuracyBonus = -0.15f;
                    EnemyReactionSpeed = 0.5f;
                    AIAggressiveness = 0.2f;
                    AIDecisionInterval = 6f;
                    AIUsesReserves = false;
                    AIFlankingEnabled = false;
                    AICoordinatesArms = false;
                    AIUsesPhasePlanning = false;
                    AICounterFlanks = false;
                    AIFeintRetreat = false;
                    AIPursuesRouting = false;
                    AIFocusFire = false;
                    PlayerIncomeMultiplier = 2f;
                    EnemyIncomeMultiplier = 0.5f;
                    break;

                case DifficultyLevel.Easy:
                    PlayerDamageMultiplier = 1.3f;
                    PlayerMoraleMultiplier = 1.3f;
                    PlayerAccuracyBonus = 0.1f;
                    EnemyDamageMultiplier = 0.7f;
                    EnemyMoraleMultiplier = 0.7f;
                    EnemyAccuracyBonus = -0.1f;
                    EnemyReactionSpeed = 0.7f;
                    AIAggressiveness = 0.3f;
                    AIDecisionInterval = 4f;
                    AIUsesReserves = false;
                    AIFlankingEnabled = true;
                    AICoordinatesArms = false;
                    AIUsesPhasePlanning = false;
                    AICounterFlanks = false;
                    AIFeintRetreat = false;
                    AIPursuesRouting = false;
                    AIFocusFire = false;
                    PlayerIncomeMultiplier = 1.5f;
                    EnemyIncomeMultiplier = 0.7f;
                    break;

                case DifficultyLevel.Normal:
                    PlayerDamageMultiplier = 1f;
                    PlayerMoraleMultiplier = 1f;
                    PlayerAccuracyBonus = 0f;
                    EnemyDamageMultiplier = 1f;
                    EnemyMoraleMultiplier = 1f;
                    EnemyAccuracyBonus = 0f;
                    EnemyReactionSpeed = 1f;
                    AIAggressiveness = 0.5f;
                    AIDecisionInterval = 3f;
                    AIUsesReserves = true;
                    AIFlankingEnabled = true;
                    AICoordinatesArms = false;
                    AIUsesPhasePlanning = false;
                    AICounterFlanks = false;
                    AIFeintRetreat = false;
                    AIPursuesRouting = false;
                    AIFocusFire = false;
                    PlayerIncomeMultiplier = 1f;
                    EnemyIncomeMultiplier = 1f;
                    break;

                case DifficultyLevel.Hard:
                    PlayerDamageMultiplier = 0.8f;
                    PlayerMoraleMultiplier = 0.8f;
                    PlayerAccuracyBonus = -0.05f;
                    EnemyDamageMultiplier = 1.3f;
                    EnemyMoraleMultiplier = 1.3f;
                    EnemyAccuracyBonus = 0.1f;
                    EnemyReactionSpeed = 1.5f;
                    AIAggressiveness = 0.7f;
                    AIDecisionInterval = 1.5f;
                    AIUsesReserves = true;
                    AIFlankingEnabled = true;
                    AICoordinatesArms = true;
                    AIUsesPhasePlanning = true;
                    AICounterFlanks = false;
                    AIFeintRetreat = false;
                    AIPursuesRouting = true;
                    AIFocusFire = true;
                    PlayerIncomeMultiplier = 0.8f;
                    EnemyIncomeMultiplier = 1.3f;
                    break;

                case DifficultyLevel.Legendary:
                    PlayerDamageMultiplier = 0.7f;
                    PlayerMoraleMultiplier = 0.7f;
                    PlayerAccuracyBonus = -0.1f;
                    EnemyDamageMultiplier = 1.5f;
                    EnemyMoraleMultiplier = 1.5f;
                    EnemyAccuracyBonus = 0.15f;
                    EnemyReactionSpeed = 2f;
                    AIAggressiveness = 0.85f;
                    AIDecisionInterval = 0.8f;
                    AIUsesReserves = true;
                    AIFlankingEnabled = true;
                    AICoordinatesArms = true;
                    AIUsesPhasePlanning = true;
                    AICounterFlanks = true;
                    AIFeintRetreat = true;
                    AIPursuesRouting = true;
                    AIFocusFire = true;
                    PlayerIncomeMultiplier = 0.6f;
                    EnemyIncomeMultiplier = 1.5f;
                    break;
            }
        }

        /// <summary>Get the damage multiplier for a given team.</summary>
        public float GetDamageMultiplier(int teamId)
        {
            return teamId == 0 ? PlayerDamageMultiplier : EnemyDamageMultiplier;
        }

        /// <summary>Get the accuracy bonus for a given team.</summary>
        public float GetAccuracyBonus(int teamId)
        {
            return teamId == 0 ? PlayerAccuracyBonus : EnemyAccuracyBonus;
        }

        /// <summary>Get the morale multiplier for a given team.</summary>
        public float GetMoraleMultiplier(int teamId)
        {
            return teamId == 0 ? PlayerMoraleMultiplier : EnemyMoraleMultiplier;
        }
    }
}
