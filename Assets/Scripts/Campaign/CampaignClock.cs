using UnityEngine;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Real-time campaign clock. Ticks once per game-day (5 real seconds at 1×).
    /// Fires OnDayTick every day and OnSeasonTick every 90 days (= 1 season).
    /// Speed: 0 (paused), 1×, 2×, 5×.
    /// </summary>
    public class CampaignClock : MonoBehaviour
    {
        public static CampaignClock Instance { get; private set; }

        // ── Configuration ──────────────────────────────────────
        [Header("Timing")]
        [Tooltip("Real-time seconds per game day at 1× speed")]
        public float SecondsPerDay = 5f;

        [Tooltip("Game days per season (Spring/Summer/Autumn/Winter)")]
        public int DaysPerSeason = 90;

        // ── State ──────────────────────────────────────────────
        private float accumulated;       // seconds accumulated towards next day
        private int speedMultiplier = 1; // 0 = paused, 1/2/5
        private bool wasPausedByPlayer;
        private int daysSinceSeasonStart; // 0..DaysPerSeason-1

        // ── Public API ─────────────────────────────────────────
        public int Speed => speedMultiplier;
        public bool IsPaused => speedMultiplier == 0;

        /// <summary>Progress 0..1 towards next day.</summary>
        public float DayProgress => SecondsPerDay > 0f ? Mathf.Clamp01(accumulated / SecondsPerDay) : 0f;

        /// <summary>Progress 0..1 towards next season tick.</summary>
        public float SeasonProgress => DaysPerSeason > 0 ? (float)daysSinceSeasonStart / DaysPerSeason : 0f;

        /// <summary>Current day within the season (1-based for display).</summary>
        public int DayInSeason => daysSinceSeasonStart + 1;

        // ── Events ─────────────────────────────────────────────
        /// <summary>Fired every game day (light processing: army movement, UI).</summary>
        public event System.Action OnDayTick;

        /// <summary>Fired every season change (heavy processing: economy, buildings, AI).</summary>
        public event System.Action OnSeasonTick;

        /// <summary>Fired every frame with day progress (for UI bars).</summary>
        public event System.Action<float> OnProgressChanged;

        /// <summary>Fired when speed changes.</summary>
        public event System.Action<int> OnSpeedChanged;

        // ── Unity lifecycle ────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (speedMultiplier <= 0) return;

            accumulated += Time.deltaTime * speedMultiplier;
            OnProgressChanged?.Invoke(DayProgress);

            // Process multiple days if frame took long or speed is very high
            while (accumulated >= SecondsPerDay)
            {
                accumulated -= SecondsPerDay;
                daysSinceSeasonStart++;
                OnDayTick?.Invoke();

                // Season boundary
                if (daysSinceSeasonStart >= DaysPerSeason)
                {
                    daysSinceSeasonStart = 0;
                    OnSeasonTick?.Invoke();
                }
            }
        }

        // ── Controls ───────────────────────────────────────────
        public void SetSpeed(int speed)
        {
            speed = Mathf.Clamp(speed, 0, 5);
            if (speed == speedMultiplier) return;
            speedMultiplier = speed;
            wasPausedByPlayer = speed == 0;
            OnSpeedChanged?.Invoke(speedMultiplier);
        }

        public void TogglePause()
        {
            if (IsPaused)
                SetSpeed(1);
            else
                SetSpeed(0);
        }

        public void Pause()   => SetSpeed(0);
        public void Play()    => SetSpeed(1);
        public void Fast()    => SetSpeed(2);
        public void Fastest() => SetSpeed(5);

        /// <summary>Reset accumulated time (e.g. on game load).</summary>
        public void ResetAccumulator()
        {
            accumulated = 0f;
            daysSinceSeasonStart = 0;
        }
    }
}
