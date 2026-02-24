using UnityEngine;
using NapoleonicWars.Units;
using NapoleonicWars.Data;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Tracks battle statistics: kills, losses, shots fired, time elapsed, etc.
    /// Singleton — created by BattleSceneSetup.
    /// </summary>
    public class BattleStatistics : MonoBehaviour
    {
        public static BattleStatistics Instance { get; private set; }

        // Player stats
        public int PlayerKills { get; private set; }
        public int PlayerLosses { get; private set; }
        public int PlayerShotsFired { get; private set; }
        public int PlayerShotsHit { get; private set; }
        public int PlayerCavalryCharges { get; private set; }
        public int PlayerCannonsFired { get; private set; }
        public int PlayerOfficersLost { get; private set; }
        public int PlayerStartingUnits { get; private set; }

        // Enemy stats
        public int EnemyKills { get; private set; }
        public int EnemyLosses { get; private set; }
        public int EnemyShotsFired { get; private set; }
        public int EnemyStartingUnits { get; private set; }

        // Global
        public float BattleDuration { get; private set; }
        public bool BattleEnded { get; private set; }
        public bool PlayerWon { get; private set; }

        private float battleStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            battleStartTime = Time.time;

            // Count starting units
            if (BattleManager.Instance != null)
            {
                foreach (var reg in BattleManager.Instance.PlayerRegiments)
                    PlayerStartingUnits += reg.AliveCount;
                foreach (var reg in BattleManager.Instance.EnemyRegiments)
                    EnemyStartingUnits += reg.AliveCount;
            }
        }

        private void Update()
        {
            if (!BattleEnded)
                BattleDuration = Time.time - battleStartTime;
        }

        // === RECORDING METHODS (called by combat systems) ===

        public void RecordKill(int killerTeamId)
        {
            if (killerTeamId == 0)
            {
                PlayerKills++;
                EnemyLosses++;
            }
            else
            {
                EnemyKills++;
                PlayerLosses++;
            }
        }

        public void RecordShotFired(int teamId)
        {
            if (teamId == 0)
                PlayerShotsFired++;
            else
                EnemyShotsFired++;
        }

        public void RecordShotHit(int teamId)
        {
            if (teamId == 0)
                PlayerShotsHit++;
        }

        public void RecordCavalryCharge(int teamId)
        {
            if (teamId == 0)
                PlayerCavalryCharges++;
        }

        public void RecordCannonFired(int teamId)
        {
            if (teamId == 0)
                PlayerCannonsFired++;
        }

        public void RecordOfficerLost(int teamId)
        {
            if (teamId == 0)
                PlayerOfficersLost++;
        }

        public void EndBattle(bool playerWon)
        {
            BattleEnded = true;
            PlayerWon = playerWon;
            BattleDuration = Time.time - battleStartTime;
        }

        // === COMPUTED STATS ===

        public float PlayerAccuracy => PlayerShotsFired > 0 ? (float)PlayerShotsHit / PlayerShotsFired : 0f;
        public float PlayerKDRatio => PlayerLosses > 0 ? (float)PlayerKills / PlayerLosses : PlayerKills;
        public float PlayerSurvivalRate => PlayerStartingUnits > 0 ? 1f - (float)PlayerLosses / PlayerStartingUnits : 1f;

        public string GetBattleDurationString()
        {
            int minutes = Mathf.FloorToInt(BattleDuration / 60f);
            int seconds = Mathf.FloorToInt(BattleDuration % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Calculate a battle rating from S to D based on performance.
        /// </summary>
        public string GetBattleRating()
        {
            if (!PlayerWon) return "D";

            float score = 0f;
            score += PlayerSurvivalRate * 40f;       // Up to 40 pts for survival
            score += Mathf.Min(PlayerAccuracy * 30f, 30f); // Up to 30 pts for accuracy
            score += Mathf.Min(PlayerKDRatio * 5f, 20f);   // Up to 20 pts for K/D
            score += Mathf.Clamp(300f / Mathf.Max(BattleDuration, 1f), 0f, 10f); // Speed bonus

            if (score >= 85f) return "S";
            if (score >= 70f) return "A";
            if (score >= 50f) return "B";
            if (score >= 30f) return "C";
            return "D";
        }
    }
}
