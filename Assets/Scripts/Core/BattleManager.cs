using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.Core
{
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Header("Army Composition")]
        [SerializeField] private int infantryRegiments = 8;
        [SerializeField] private int cavalryRegiments = 3;
        [SerializeField] private int artilleryRegiments = 2;
        [SerializeField] private int infantryPerRegiment = 400; // Massively increased for qualitative battles
        [SerializeField] private int cavalryPerRegiment = 120;
        [SerializeField] private int artilleryPerRegiment = 24;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnSpreadX = 600f; // Wider to accommodate large regiments
        [SerializeField] private float playerSpawnZ = -200f; // More distance between armies
        [SerializeField] private float enemySpawnZ = 200f;
        [SerializeField] private float cavalryOffsetZ = -40f;
        [SerializeField] private float artilleryOffsetZ = 30f;

        [Header("Deployment Settings")]
        [SerializeField] private float deploymentZoneDepth = 40f; // How far units can move during deployment
        [SerializeField] private float deploymentTimeLimit = 120f; // Max time for deployment in seconds
        [SerializeField] private bool autoStartWhenReady = false; // Auto-start when both ready

        private List<Regiment> playerRegiments = new List<Regiment>();
        private List<Regiment> enemyRegiments = new List<Regiment>();

        public List<Regiment> PlayerRegiments => playerRegiments;
        public List<Regiment> EnemyRegiments => enemyRegiments;

        // Deployment state
        private bool playerReady = false;
        private bool aiReady = false;
        private float deploymentTimer = 0f;
        private bool deploymentPhaseActive = false;

        public bool IsDeploymentPhase => deploymentPhaseActive;
        public bool IsBattleActive => !deploymentPhaseActive;
        public bool PlayerReady => playerReady;
        public bool AIReady => aiReady;
        public float DeploymentTimeRemaining => Mathf.Max(0f, deploymentTimeLimit - deploymentTimer);
        public bool BothPlayersReady => playerReady && aiReady;

        // Events
        public delegate void DeploymentEvent();
        public event DeploymentEvent OnPlayerReadyChanged;
        public event DeploymentEvent OnAIReadyChanged;
        public event DeploymentEvent OnDeploymentEnded;

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
            SpawnArmies();
            StartDeployment();
        }

        private void StartDeployment()
        {
            deploymentPhaseActive = true;
            deploymentTimer = 0f;
            playerReady = false;
            aiReady = false;

            if (GameManager.Instance != null)
                GameManager.Instance.StartDeployment();

            Debug.Log("[BattleManager] Deployment phase started. Move your units and press Ready when done.");
        }

        public void SetPlayerReady(bool ready)
        {
            if (playerReady == ready) return;
            playerReady = ready;
            OnPlayerReadyChanged?.Invoke();

            if (ready)
                Debug.Log("[BattleManager] Player is ready!");
            else
                Debug.Log("[BattleManager] Player cancelled ready.");

            TryStartBattle();
        }

        public void SetAIReady(bool ready)
        {
            if (aiReady == ready) return;
            aiReady = ready;
            OnAIReadyChanged?.Invoke();

            if (ready)
                Debug.Log("[BattleManager] AI is ready!");

            TryStartBattle();
        }

        private void TryStartBattle()
        {
            if (!deploymentPhaseActive) return;

            if (BothPlayersReady)
            {
                EndDeploymentAndStartBattle();
            }
            else if (autoStartWhenReady && (playerReady || aiReady))
            {
                // Optional: auto-start if one player is ready (configurable)
            }
        }

        public void EndDeploymentAndStartBattle()
        {
            if (!deploymentPhaseActive) return;

            deploymentPhaseActive = false;
            OnDeploymentEnded?.Invoke();

            // Notify deployment boundary that battle started
            if (DeploymentBoundary.Instance != null)
                DeploymentBoundary.Instance.StartBattle();

            if (GameManager.Instance != null)
                GameManager.Instance.EndDeploymentAndStartBattle();

            Debug.Log("[BattleManager] Deployment ended - Battle begins!");
        }

        public void TogglePlayerReady()
        {
            SetPlayerReady(!playerReady);
        }

        private void SpawnArmies()
        {
            FactionType attackerFaction = (FactionType)PlayerPrefs.GetInt("Battle_AttackerFaction", (int)FactionType.France);
            FactionType defenderFaction = (FactionType)PlayerPrefs.GetInt("Battle_DefenderFaction", (int)FactionType.Britain);
            
            // Try campaign-aware spawn first (reads per-regiment data from PlayerPrefs)
            bool usedCampaignData = false;
            int attackerRegCount = PlayerPrefs.GetInt("Battle_AttackerRegiments", 0);
            int defenderRegCount = PlayerPrefs.GetInt("Battle_DefenderRegiments", 0);
            
            if (attackerRegCount > 0)
            {
                SpawnCampaignArmy("Attacker", attackerFaction, 0, playerSpawnZ, 0f, playerRegiments);
                usedCampaignData = true;
            }
            else
            {
                SpawnArmy(attackerFaction, 0, playerSpawnZ, 0f, playerRegiments);
            }
            
            if (defenderRegCount > 0)
            {
                SpawnCampaignArmy("Defender", defenderFaction, 1, enemySpawnZ, 180f, enemyRegiments);
            }
            else
            {
                SpawnArmy(defenderFaction, 1, enemySpawnZ, 180f, enemyRegiments);
            }

            int totalPlayer = 0, totalEnemy = 0;
            foreach (var r in playerRegiments) totalPlayer += r.AliveCount;
            foreach (var r in enemyRegiments) totalEnemy += r.AliveCount;
            Debug.Log($"Battle: {totalPlayer} vs {totalEnemy} = {totalPlayer + totalEnemy} total units.{(usedCampaignData ? " (campaign data)" : "")}");
        }

        /// <summary>Spawn army using per-regiment campaign data from PlayerPrefs.</summary>
        private void SpawnCampaignArmy(string prefix, FactionType faction, int teamId, float baseZ, float rotation, List<Regiment> regList)
        {
            int regCount = PlayerPrefs.GetInt($"Battle_{prefix}Regiments", 0);
            float spacing = spawnSpreadX / Mathf.Max(regCount - 1, 1);
            float startX = -spawnSpreadX * 0.5f;
            
            for (int i = 0; i < regCount; i++)
            {
                string key = $"Battle_{prefix}_Reg{i}";
                UnitType unitType = (UnitType)PlayerPrefs.GetInt($"{key}_Type", (int)UnitType.LineInfantry);
                int size = PlayerPrefs.GetInt($"{key}_Size", 60);
                int rank = PlayerPrefs.GetInt($"{key}_Rank", 0);
                string regName = PlayerPrefs.GetString($"{key}_Name", $"{faction} Regiment {i + 1}");
                
                UnitData data = UnitFactory.CreateUnitData(unitType, faction);
                
                // Position based on unit category
                bool isCav = unitType == UnitType.Cavalry || unitType == UnitType.Hussar || 
                             unitType == UnitType.Lancer || unitType == UnitType.GuardCavalry;
                bool isArt = unitType == UnitType.Artillery || unitType == UnitType.GuardArtillery;
                
                float z = baseZ;
                float x = startX + i * spacing;
                if (isCav)
                {
                    z += (teamId == 0 ? cavalryOffsetZ : -cavalryOffsetZ);
                    x = ((i % 2 == 0) ? -1f : 1f) * (spawnSpreadX * 0.5f + 15f);
                }
                else if (isArt)
                {
                    z += (teamId == 0 ? artilleryOffsetZ : -artilleryOffsetZ);
                }
                
                Vector3 pos = new Vector3(x, 0f, z);
                Regiment reg = SpawnRegiment(data, pos, teamId, regName, size, rotation, rank);
                
                // Auto-configure formation
                if (isCav) reg.SetFormation(FormationType.Column);
                else if (data.canVolleyFire) reg.SetVolleyMode(true);
                
                regList.Add(reg);
            }
        }

        private void SpawnArmy(FactionType faction, int teamId, float baseZ, float rotation, List<Regiment> regList)
        {
            int totalInfRegiments = infantryRegiments;
            float infantrySpacing = spawnSpreadX / Mathf.Max(totalInfRegiments - 1, 1);
            float startX = -spawnSpreadX * 0.5f;

            // Infantry line (front)
            UnitData lineData = UnitFactory.CreateLineInfantry(faction);
            for (int i = 0; i < totalInfRegiments; i++)
            {
                Vector3 pos = new Vector3(startX + i * infantrySpacing, 0f, baseZ);
                string name = $"{faction} Line Infantry {i + 1}";
                Regiment reg = SpawnRegiment(lineData, pos, teamId, name, infantryPerRegiment, rotation);
                reg.SetVolleyMode(true);
                regList.Add(reg);
            }

            // Cavalry (flanks, behind infantry)
            UnitData cavData = UnitFactory.CreateCavalry(faction);
            float cavZ = baseZ + (teamId == 0 ? cavalryOffsetZ : -cavalryOffsetZ);
            for (int i = 0; i < cavalryRegiments; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float cavX = side * (spawnSpreadX * 0.5f + 15f);
                Vector3 pos = new Vector3(cavX, 0f, cavZ);
                string name = $"{faction} Cavalry {i + 1}";
                Regiment reg = SpawnRegiment(cavData, pos, teamId, name, cavalryPerRegiment, rotation);
                reg.SetFormation(FormationType.Column);
                regList.Add(reg);
            }

            // Artillery (behind infantry)
            UnitData artData = UnitFactory.CreateArtillery(faction);
            float artZ = baseZ + (teamId == 0 ? artilleryOffsetZ : -artilleryOffsetZ);
            float artSpacing = spawnSpreadX * 0.6f / Mathf.Max(artilleryRegiments - 1, 1);
            float artStartX = -spawnSpreadX * 0.3f;
            for (int i = 0; i < artilleryRegiments; i++)
            {
                Vector3 pos = new Vector3(artStartX + i * artSpacing, 0f, artZ);
                string name = $"{faction} Artillery Battery {i + 1}";
                Regiment reg = SpawnRegiment(artData, pos, teamId, name, artilleryPerRegiment, rotation);
                reg.SetFormation(FormationType.Line);
                regList.Add(reg);
            }
        }

        private Regiment SpawnRegiment(UnitData data, Vector3 position, int teamId, string name, int unitCount, float yRotation, int rank = 0)
        {
            // Sample terrain height so units spawn on the surface
            position.y = GetTerrainHeight(position);

            GameObject regGO = new GameObject(name);
            regGO.transform.position = position;
            if (Mathf.Abs(yRotation) > 0.01f)
                regGO.transform.Rotate(Vector3.up, yRotation);

            Regiment regiment = regGO.AddComponent<Regiment>();
            regiment.TeamId = teamId;
            regiment.Initialize(data, unitCount, teamId);
            
            // Apply regiment rank bonuses from campaign data
            if (rank > 0)
                regiment.SetRegimentRank(rank);

            return regiment;
        }

        private float battleCheckTimer;

        private void Update()
        {
            if (deploymentPhaseActive)
            {
                UpdateDeployment();
            }
            else
            {
                battleCheckTimer -= Time.deltaTime;
                if (battleCheckTimer <= 0f)
                {
                    battleCheckTimer = 0.5f;
                    CheckBattleEnd();
                }
            }
        }

        private void UpdateDeployment()
        {
            deploymentTimer += Time.deltaTime;

            // Auto-start battle when time limit reached
            if (deploymentTimer >= deploymentTimeLimit)
            {
                Debug.Log("[BattleManager] Deployment time limit reached!");
                EndDeploymentAndStartBattle();
                return;
            }

            // AI deployment logic - AI becomes ready after some time or when positioned
            if (!aiReady)
            {
                // Simple AI: becomes ready after 10-20 seconds or when it has moved units
                float aiReadyTime = 10f + Random.Range(0f, 10f);
                if (deploymentTimer >= aiReadyTime)
                {
                    SetAIReady(true);
                }
            }
        }

        // Check if a regiment is within the deployment zone
        public bool IsWithinDeploymentZone(Vector3 position, int teamId)
        {
            float baseZ = teamId == 0 ? playerSpawnZ : enemySpawnZ;
            float allowedMinZ = baseZ - deploymentZoneDepth * 0.5f;
            float allowedMaxZ = baseZ + deploymentZoneDepth * 0.5f;

            return position.z >= allowedMinZ && position.z <= allowedMaxZ;
        }

        private void CheckBattleEnd()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Battle) return;

            int playerAlive = 0;
            int enemyAlive = 0;

            foreach (var reg in playerRegiments)
                playerAlive += reg.CachedAliveCount;
            foreach (var reg in enemyRegiments)
                enemyAlive += reg.CachedAliveCount;

            if (playerAlive <= 0)
            {
                if (BattleStatistics.Instance != null)
                    BattleStatistics.Instance.EndBattle(false);
                if (BattleSoundManager.Instance != null)
                    BattleSoundManager.Instance.PlayDefeatMusic();
                GameManager.Instance.EndBattle(false);
                Debug.Log("DEFEAT! All player units destroyed.");
            }
            else if (enemyAlive <= 0)
            {
                if (BattleStatistics.Instance != null)
                    BattleStatistics.Instance.EndBattle(true);
                if (BattleSoundManager.Instance != null)
                    BattleSoundManager.Instance.PlayVictoryMusic();
                GameManager.Instance.EndBattle(true);
                Debug.Log("VICTORY! All enemy units destroyed.");
            }
        }

        private static Terrain cachedTerrain;

        public static float GetTerrainHeight(Vector3 worldPos)
        {
            // Try active terrain first
            if (cachedTerrain == null) cachedTerrain = Terrain.activeTerrain;
            
            if (cachedTerrain != null)
                return cachedTerrain.SampleHeight(worldPos) + cachedTerrain.transform.position.y;

            // Fallback: raycast down
            if (Physics.Raycast(new Vector3(worldPos.x, 500f, worldPos.z), Vector3.down, out RaycastHit hit, 1000f))
                return hit.point.y;

            return 0f;
        }

        public (int playerAlive, int enemyAlive) GetArmyCounts()
        {
            int playerAlive = 0;
            int enemyAlive = 0;

            foreach (var reg in playerRegiments)
                playerAlive += reg.CachedAliveCount;
            foreach (var reg in enemyRegiments)
                enemyAlive += reg.CachedAliveCount;

            return (playerAlive, enemyAlive);
        }
    }
}
