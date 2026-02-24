using UnityEngine;
using UnityEngine.SceneManagement;
using NapoleonicWars.UI;

namespace NapoleonicWars.Core
{
    public enum GameState
    {
        MainMenu,
        Campaign,
        Deployment,  // Pre-battle unit placement phase
        Battle,
        Paused,
        Victory,
        Defeat
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.MainMenu;

        public GameState CurrentState
        {
            get => currentState;
            private set
            {
                var previousState = currentState;
                currentState = value;
                OnGameStateChanged?.Invoke(previousState, currentState);
            }
        }

        public delegate void GameStateChanged(GameState previousState, GameState newState);
        public event GameStateChanged OnGameStateChanged;

        [Header("Battle Settings")]
        [SerializeField] private int maxUnitsPerPlayer = 2500;
        [SerializeField] private float battleTimeLimit = 1800f; // 30 minutes

        public int MaxUnitsPerPlayer => maxUnitsPerPlayer;
        public float BattleTimeLimit => battleTimeLimit;

        // Battle transition data (set before loading Battle scene)
        public string AttackerArmyId { get; set; }
        public string DefenderArmyId { get; set; }
        public string BattleProvinceId { get; set; }
        public bool BattleResultVictory { get; set; }

        private float battleTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Enable incremental GC to reduce lag spikes
            // This spreads GC work across multiple frames instead of one big pause
            UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Enabled;
            
            // Set target framerate to reduce unnecessary CPU usage
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (currentState == GameState.Battle)
            {
                battleTimer += Time.deltaTime;

                if (battleTimer >= battleTimeLimit)
                {
                    EndBattle(false);
                }
            }
        }

        public void StartCampaign()
        {
            CurrentState = GameState.Campaign;
            Time.timeScale = 1f;
        }

        public void StartBattle()
        {
            battleTimer = 0f;
            CurrentState = GameState.Battle;
            Time.timeScale = 1f;
        }

        public void StartDeployment()
        {
            CurrentState = GameState.Deployment;
            Time.timeScale = 1f;
        }

        public void EndDeploymentAndStartBattle()
        {
            if (currentState == GameState.Deployment)
            {
                StartBattle();
            }
        }

        public void PauseBattle()
        {
            if (currentState == GameState.Battle)
            {
                CurrentState = GameState.Paused;
                Time.timeScale = 0f;
            }
        }

        public void ResumeBattle()
        {
            if (currentState == GameState.Paused)
            {
                CurrentState = GameState.Battle;
                Time.timeScale = 1f;
            }
        }

        public void TogglePause()
        {
            if (currentState == GameState.Battle)
                PauseBattle();
            else if (currentState == GameState.Paused)
                ResumeBattle();
        }

        public void EndBattle(bool victory)
        {
            BattleResultVictory = victory;
            CurrentState = victory ? GameState.Victory : GameState.Defeat;
            Time.timeScale = 1f;
        }

        public void TransitionToBattle(string attackerArmyId, string defenderArmyId, string provinceId)
        {
            AttackerArmyId = attackerArmyId;
            DefenderArmyId = defenderArmyId;
            BattleProvinceId = provinceId;
            CurrentState = GameState.Battle;
            LoadingScreenUI.LoadSceneWithScreen("Battle");
        }

        public void ReturnToCampaign()
        {
            CurrentState = GameState.Campaign;
            Time.timeScale = 1f;
            LoadingScreenUI.LoadSceneWithScreen("Campaign");
        }

        public void ReturnToMenu()
        {
            CurrentState = GameState.MainMenu;
            Time.timeScale = 1f;
            LoadingScreenUI.LoadSceneWithScreen("MainMenu");
        }

        public void SetBattleSpeed(float speed)
        {
            if (currentState == GameState.Battle)
            {
                Time.timeScale = Mathf.Clamp(speed, 0.5f, 3f);
            }
        }
    }
}
