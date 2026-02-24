using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Units;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Manages siege battles - playable battles where attackers assault fortified positions.
    /// Handles wall sections, gates, towers, and capture points.
    /// </summary>
    public class SiegeBattleManager : MonoBehaviour
    {
        public static SiegeBattleManager Instance { get; private set; }

        [Header("Siege Settings")]
        [SerializeField] private float battleTimeLimit = 1800f; // 30 minutes
        [SerializeField] private float captureTime = 30f; // Seconds to capture a point

        [Header("Fortification Prefabs")]
        [SerializeField] private GameObject wallSectionPrefab;
        [SerializeField] private GameObject gatePrefab;
        [SerializeField] private GameObject towerPrefab;

        // Battle state
        private List<Regiment> attackerRegiments = new List<Regiment>();
        private List<Regiment> defenderRegiments = new List<Regiment>();
        private List<WallSection> wallSections = new List<WallSection>();
        private List<TowerDefense> towers = new List<TowerDefense>();
        private GateController mainGate;

        // Capture points
        private List<CapturePoint> capturePoints = new List<CapturePoint>();
        private int attackerCapturedPoints = 0;
        private int totalCapturePoints = 0;

        // Siege data from campaign
        private SiegeData currentSiege;
        private FortificationData fortification;

        // Battle timer
        private float battleTimer;
        private bool battleActive;

        public List<Regiment> AttackerRegiments => attackerRegiments;
        public List<Regiment> DefenderRegiments => defenderRegiments;
        public bool BattleActive => battleActive;
        public float BattleTimeRemaining => Mathf.Max(0f, battleTimeLimit - battleTimer);

        // Events
        public delegate void SiegeBattleEvent();
        public event SiegeBattleEvent OnBattleStart;
        public event SiegeBattleEvent OnBattleEnd;
        public event SiegeBattleEvent OnWallBreached;
        public event SiegeBattleEvent OnGateDestroyed;
        public event SiegeBattleEvent OnPointCaptured;

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
            InitializeSiegeBattle();
        }

        private void Update()
        {
            if (!battleActive) return;

            battleTimer += Time.deltaTime;

            // Update capture points
            UpdateCapturePoints();

            // Check victory conditions
            CheckVictoryConditions();

            // Time limit
            if (battleTimer >= battleTimeLimit)
            {
                EndBattle(false); // Defender wins on timeout
            }
        }

        #region Initialization

        private void InitializeSiegeBattle()
        {
            // Get siege data from GameManager
            if (GameManager.Instance == null)
            {
                Debug.LogError("[SiegeBattle] No GameManager found!");
                return;
            }

            // Load siege data (would be passed from campaign)
            // For now, create default fortification
            fortification = new FortificationData(3); // Level 3 fortress

            // Generate the battlefield
            GenerateFortifications();

            // Spawn armies
            SpawnArmies();

            // Create capture points
            CreateCapturePoints();

            // Start battle
            battleActive = true;
            battleTimer = 0f;
            OnBattleStart?.Invoke();

            Debug.Log("[SiegeBattle] Siege battle started!");
        }

        private void GenerateFortifications()
        {
            // Create wall sections in a square around the city center
            float wallLength = 80f;
            float wallHeight = 8f;
            int sectionsPerSide = 4;

            Vector3 center = Vector3.zero;

            // North wall
            CreateWallLine(center + Vector3.forward * wallLength / 2, Vector3.right, sectionsPerSide, wallHeight);
            
            // South wall
            CreateWallLine(center - Vector3.forward * wallLength / 2, Vector3.right, sectionsPerSide, wallHeight);
            
            // East wall
            CreateWallLine(center + Vector3.right * wallLength / 2, Vector3.forward, sectionsPerSide, wallHeight);
            
            // West wall (with gate)
            CreateWallLine(center - Vector3.right * wallLength / 2, Vector3.forward, sectionsPerSide, wallHeight, true);

            // Create towers at corners
            CreateTower(center + new Vector3(wallLength / 2, 0, wallLength / 2));
            CreateTower(center + new Vector3(-wallLength / 2, 0, wallLength / 2));
            CreateTower(center + new Vector3(wallLength / 2, 0, -wallLength / 2));
            CreateTower(center + new Vector3(-wallLength / 2, 0, -wallLength / 2));

            Debug.Log($"[SiegeBattle] Created {wallSections.Count} wall sections, {towers.Count} towers");
        }

        private void CreateWallLine(Vector3 start, Vector3 direction, int sections, float height, bool hasGate = false)
        {
            float sectionWidth = 20f;
            int gateSection = sections / 2;

            for (int i = 0; i < sections; i++)
            {
                Vector3 pos = start + direction * (i - sections / 2f + 0.5f) * sectionWidth;

                if (hasGate && i == gateSection)
                {
                    // Create gate instead of wall
                    CreateGate(pos, direction);
                }
                else
                {
                    CreateWallSection(pos, direction, height);
                }
            }
        }

        private void CreateWallSection(Vector3 position, Vector3 facing, float height)
        {
            GameObject wallGO;
            
            if (wallSectionPrefab != null)
            {
                wallGO = Instantiate(wallSectionPrefab, position, Quaternion.LookRotation(facing));
            }
            else
            {
                // Create primitive wall
                wallGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallGO.transform.position = position + Vector3.up * height / 2;
                wallGO.transform.localScale = new Vector3(20f, height, 2f);
                wallGO.transform.rotation = Quaternion.LookRotation(facing);
                
                // Color based on fortification level
                var renderer = wallGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.5f, 0.4f, 0.3f); // Stone color
                }
            }

            wallGO.name = $"WallSection_{wallSections.Count}";

            // Add wall section component
            WallSection wall = wallGO.AddComponent<WallSection>();
            wall.Initialize(fortification.maxWallHP / 4f, height); // HP per section
            wall.OnBreached += OnWallSectionBreached;

            wallSections.Add(wall);
        }

        private void CreateGate(Vector3 position, Vector3 facing)
        {
            GameObject gateGO;

            if (gatePrefab != null)
            {
                gateGO = Instantiate(gatePrefab, position, Quaternion.LookRotation(facing));
            }
            else
            {
                // Create primitive gate
                gateGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gateGO.transform.position = position + Vector3.up * 4f;
                gateGO.transform.localScale = new Vector3(8f, 8f, 1f);
                gateGO.transform.rotation = Quaternion.LookRotation(facing);

                var renderer = gateGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.3f, 0.2f, 0.1f); // Wood color
                }
            }

            gateGO.name = "MainGate";

            // Add gate controller
            mainGate = gateGO.AddComponent<GateController>();
            mainGate.Initialize(fortification.maxGateHP);
            mainGate.OnGateDestroyed += OnGateDestroyedHandler;
        }

        private void CreateTower(Vector3 position)
        {
            GameObject towerGO;

            if (towerPrefab != null)
            {
                towerGO = Instantiate(towerPrefab, position, Quaternion.identity);
            }
            else
            {
                // Create primitive tower
                towerGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                towerGO.transform.position = position + Vector3.up * 6f;
                towerGO.transform.localScale = new Vector3(6f, 12f, 6f);

                var renderer = towerGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.45f, 0.35f, 0.25f);
                }
            }

            towerGO.name = $"Tower_{towers.Count}";

            // Add tower defense component
            TowerDefense tower = towerGO.AddComponent<TowerDefense>();
            tower.Initialize(fortification.TowerDamage, 50f); // Damage and range

            towers.Add(tower);
        }

        private void CreateCapturePoints()
        {
            // Main capture point - city center
            CreateCapturePoint(Vector3.zero, "City Center", 2f); // Worth 2 points

            // Gate capture point
            if (mainGate != null)
            {
                CreateCapturePoint(mainGate.transform.position, "Main Gate", 1f);
            }

            // Wall breach points will be created dynamically when walls are breached

            totalCapturePoints = capturePoints.Count;
        }

        private void CreateCapturePoint(Vector3 position, string pointName, float value)
        {
            GameObject pointGO = new GameObject($"CapturePoint_{pointName}");
            pointGO.transform.position = position;

            // Visual indicator
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.transform.SetParent(pointGO.transform);
            indicator.transform.localPosition = Vector3.up * 0.1f;
            indicator.transform.localScale = new Vector3(5f, 0.2f, 5f);
            
            var renderer = indicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 1f, 0f, 0.5f); // Yellow, semi-transparent
            }

            // Remove collider from indicator
            var col = indicator.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Add capture point component
            CapturePoint point = pointGO.AddComponent<CapturePoint>();
            point.Initialize(pointName, captureTime, value);
            point.OnCaptured += OnPointCapturedHandler;

            capturePoints.Add(point);
        }

        #endregion

        #region Army Spawning

        private void SpawnArmies()
        {
            // Spawn attackers outside the walls
            SpawnAttackers();

            // Spawn defenders inside/on walls
            SpawnDefenders();
        }

        private void SpawnAttackers()
        {
            Vector3 spawnCenter = new Vector3(-80f, 0f, 0f); // Outside west wall
            float spread = 60f;

            // Create attacker regiments
            int regimentCount = 5;
            for (int i = 0; i < regimentCount; i++)
            {
                Vector3 pos = spawnCenter + new Vector3(
                    Random.Range(-10f, 10f),
                    0f,
                    (i - regimentCount / 2f) * (spread / regimentCount)
                );

                Regiment reg = CreateRegiment($"Attacker_{i}", UnitType.LineInfantry, 60, 0, pos);
                attackerRegiments.Add(reg);
            }

            // Add artillery
            Vector3 artilleryPos = spawnCenter + new Vector3(-20f, 0f, 0f);
            Regiment artillery = CreateRegiment("Siege Artillery", UnitType.Artillery, 8, 0, artilleryPos);
            attackerRegiments.Add(artillery);

            Debug.Log($"[SiegeBattle] Spawned {attackerRegiments.Count} attacker regiments");
        }

        private void SpawnDefenders()
        {
            // Spawn some on walls
            foreach (var wall in wallSections)
            {
                if (Random.value > 0.5f) continue; // Not all walls manned

                Vector3 pos = wall.transform.position + Vector3.up * 8f; // On top of wall
                Regiment reg = CreateRegiment($"WallDefender", UnitType.LineInfantry, 20, 1, pos);
                defenderRegiments.Add(reg);
            }

            // Spawn reserve in city center
            Vector3 centerPos = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = centerPos + new Vector3(
                    Random.Range(-15f, 15f),
                    0f,
                    Random.Range(-15f, 15f)
                );

                Regiment reg = CreateRegiment($"Reserve_{i}", UnitType.LineInfantry, 40, 1, pos);
                defenderRegiments.Add(reg);
            }

            Debug.Log($"[SiegeBattle] Spawned {defenderRegiments.Count} defender regiments");
        }

        private Regiment CreateRegiment(string name, UnitType type, int size, int team, Vector3 position)
        {
            GameObject regGO = new GameObject($"Regiment_{name}");
            regGO.transform.position = position;

            Regiment reg = regGO.AddComponent<Regiment>();
            
            // Get or create unit data
            UnitData data = GetUnitData(type, team);
            if (data != null)
            {
                reg.Initialize(data, size, team);
            }

            return reg;
        }

        private UnitData GetUnitData(UnitType type, int team)
        {
            // Try to load from resources
            string path = $"UnitData/{type}";
            UnitData data = Resources.Load<UnitData>(path);

            if (data == null)
            {
                // Create runtime data
                data = ScriptableObject.CreateInstance<UnitData>();
                data.unitType = type;
                data.unitName = type.ToString();
                data.factionColor = team == 0 ? Color.blue : Color.red;
                
                // Set stats based on type
                switch (type)
                {
                    case UnitType.LineInfantry:
                        data.maxHealth = 100f;
                        data.moveSpeed = 3.5f;
                        data.attackDamage = 25f;
                        data.attackRange = 150f;
                        break;
                    case UnitType.Artillery:
                        data.maxHealth = 80f;
                        data.moveSpeed = 1.5f;
                        data.attackDamage = 100f;
                        data.attackRange = 300f;
                        data.splashRadius = 5f;
                        break;
                }
            }

            return data;
        }

        #endregion

        #region Battle Logic

        private void UpdateCapturePoints()
        {
            foreach (var point in capturePoints)
            {
                if (point.IsCaptured) continue;

                // Check for units in capture zone
                int attackersInZone = CountUnitsInRadius(point.transform.position, 10f, 0);
                int defendersInZone = CountUnitsInRadius(point.transform.position, 10f, 1);

                point.UpdateCapture(attackersInZone, defendersInZone, Time.deltaTime);
            }
        }

        private int CountUnitsInRadius(Vector3 center, float radius, int team)
        {
            int count = 0;
            var regiments = team == 0 ? attackerRegiments : defenderRegiments;

            foreach (var reg in regiments)
            {
                foreach (var unit in reg.Units)
                {
                    if (unit == null || unit.CurrentState == UnitState.Dead) continue;
                    
                    if (Vector3.Distance(unit.transform.position, center) <= radius)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void CheckVictoryConditions()
        {
            // Attacker wins if:
            // 1. All capture points captured
            // 2. All defenders eliminated

            // Check capture points
            int captured = 0;
            foreach (var point in capturePoints)
            {
                if (point.IsCaptured && point.CapturingTeam == 0)
                    captured++;
            }

            if (captured >= totalCapturePoints)
            {
                EndBattle(true); // Attacker wins
                return;
            }

            // Check defender elimination
            int defenderAlive = 0;
            foreach (var reg in defenderRegiments)
            {
                defenderAlive += reg.AliveCount;
            }

            if (defenderAlive == 0)
            {
                EndBattle(true); // Attacker wins
                return;
            }

            // Check attacker elimination
            int attackerAlive = 0;
            foreach (var reg in attackerRegiments)
            {
                attackerAlive += reg.AliveCount;
            }

            if (attackerAlive == 0)
            {
                EndBattle(false); // Defender wins
            }
        }

        #endregion

        #region Event Handlers

        private void OnWallSectionBreached(WallSection wall)
        {
            Debug.Log($"[SiegeBattle] Wall section breached at {wall.transform.position}!");
            
            // Create capture point at breach
            CreateCapturePoint(wall.transform.position, "Breach", 0.5f);
            totalCapturePoints++;

            OnWallBreached?.Invoke();
        }

        private void OnGateDestroyedHandler()
        {
            Debug.Log("[SiegeBattle] Main gate destroyed!");
            OnGateDestroyed?.Invoke();
        }

        private void OnPointCapturedHandler(CapturePoint point, int team)
        {
            if (team == 0) // Attacker
            {
                attackerCapturedPoints++;
                Debug.Log($"[SiegeBattle] Attackers captured {point.PointName}! ({attackerCapturedPoints}/{totalCapturePoints})");
            }
            
            OnPointCaptured?.Invoke();
        }

        #endregion

        #region Battle End

        public void EndBattle(bool attackerVictory)
        {
            if (!battleActive) return;

            battleActive = false;
            OnBattleEnd?.Invoke();

            Debug.Log($"[SiegeBattle] Battle ended! {(attackerVictory ? "Attackers" : "Defenders")} win!");

            // Report result to campaign
            if (currentSiege != null && SiegeManager.Instance != null)
            {
                SiegeManager.Instance.ResolveAssault(currentSiege.siegeId, attackerVictory);
            }

            // Return to campaign after delay
            StartCoroutine(ReturnToCampaign(attackerVictory));
        }

        private System.Collections.IEnumerator ReturnToCampaign(bool attackerVictory)
        {
            yield return new WaitForSeconds(5f);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.BattleResultVictory = attackerVictory;
                GameManager.Instance.ReturnToCampaign();
            }
        }

        #endregion

        #region Public Commands

        /// <summary>
        /// Order attackers to assault the walls with ladders
        /// </summary>
        public void OrderWallAssault(Regiment regiment, WallSection targetWall)
        {
            if (regiment == null || targetWall == null) return;

            Vector3 wallTop = targetWall.transform.position + Vector3.up * targetWall.WallHeight;
            regiment.MoveRegiment(wallTop);
        }

        /// <summary>
        /// Order artillery to bombard a wall section
        /// </summary>
        public void OrderBombardWall(Regiment artillery, WallSection targetWall)
        {
            if (artillery == null || targetWall == null) return;
            if (artillery.UnitData.unitType != UnitType.Artillery) return;

            // Artillery will automatically target the wall
            // Wall takes damage over time from artillery fire
            targetWall.TakeDamage(artillery.UnitData.attackDamage);
        }

        /// <summary>
        /// Order ram to attack the gate
        /// </summary>
        public void OrderGateAssault(Regiment regiment)
        {
            if (regiment == null || mainGate == null) return;

            regiment.MoveRegiment(mainGate.transform.position);
            // When units reach gate, they will attack it
        }

        #endregion
    }
}
