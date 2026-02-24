using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Naval
{
    /// <summary>
    /// Manages naval battles - ship combat, wind, and victory conditions.
    /// </summary>
    public class NavalBattleManager : MonoBehaviour
    {
        public static NavalBattleManager Instance { get; private set; }

        [Header("Battle Settings")]
        [SerializeField] private float battleTimeLimit = 1800f;
        [SerializeField] private Vector2 battleAreaSize = new Vector2(500f, 500f);

        [Header("Wind")]
        [SerializeField] private float windDirection = 0f; // Degrees, 0 = North
        [SerializeField] private float windStrength = 1f; // 0-2
        [SerializeField] private float windChangeInterval = 60f;

        // Fleets
        private List<Ship> playerFleet = new List<Ship>();
        private List<Ship> enemyFleet = new List<Ship>();
        private List<Ship> selectedShips = new List<Ship>();

        // Battle state
        private float battleTimer;
        private bool battleActive;
        private float windChangeTimer;
        private Camera mainCamera;

        public List<Ship> PlayerFleet => playerFleet;
        public List<Ship> EnemyFleet => enemyFleet;
        public List<Ship> SelectedShips => selectedShips;
        public float WindDirection => windDirection;
        public float WindStrength => windStrength;
        public bool BattleActive => battleActive;
        public float BattleTimeRemaining => Mathf.Max(0f, battleTimeLimit - battleTimer);

        // Events
        public delegate void NavalBattleEvent();
        public event NavalBattleEvent OnBattleStart;
        public event NavalBattleEvent OnBattleEnd;
        public event NavalBattleEvent OnWindChanged;

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
            mainCamera = Camera.main;
            InitializeNavalBattle();
        }

        private void Update()
        {
            if (!battleActive) return;

            battleTimer += Time.deltaTime;
            
            UpdateWind();
            UpdateAI();
            CheckVictoryConditions();

            if (battleTimer >= battleTimeLimit)
            {
                EndBattle(false); // Draw/defender wins
            }

            HandleInput();
        }

        #region Initialization

        private void InitializeNavalBattle()
        {
            // Set initial wind
            windDirection = Random.Range(0f, 360f);
            windStrength = Random.Range(0.5f, 1.5f);
            windChangeTimer = windChangeInterval;

            // Create water plane
            CreateWaterPlane();

            // Spawn fleets
            SpawnFleets();

            battleActive = true;
            battleTimer = 0f;
            OnBattleStart?.Invoke();

            Debug.Log($"[NavalBattle] Battle started! Wind: {windDirection:F0}° at {windStrength:F1} strength");
        }

        private void CreateWaterPlane()
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Ocean";
            water.transform.position = Vector3.zero;
            water.transform.localScale = new Vector3(battleAreaSize.x / 10f, 1f, battleAreaSize.y / 10f);

            var renderer = water.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.1f, 0.3f, 0.5f);
            }

            // Remove collider
            var col = water.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void SpawnFleets()
        {
            // Player fleet (west side)
            Vector3 playerSpawn = new Vector3(-battleAreaSize.x * 0.3f, 0f, 0f);
            SpawnFleet(playerSpawn, 0, playerFleet, FactionType.Britain);

            // Enemy fleet (east side)
            Vector3 enemySpawn = new Vector3(battleAreaSize.x * 0.3f, 0f, 0f);
            SpawnFleet(enemySpawn, 1, enemyFleet, FactionType.France);

            Debug.Log($"[NavalBattle] Spawned {playerFleet.Count} player ships, {enemyFleet.Count} enemy ships");
        }

        private void SpawnFleet(Vector3 center, int team, List<Ship> fleet, FactionType faction)
        {
            // Spawn a mix of ships
            SpawnShip(center + new Vector3(0, 0, 30), team, ShipType.ShipOfTheLine, faction, fleet);
            SpawnShip(center + new Vector3(0, 0, 0), team, ShipType.ShipOfTheLine, faction, fleet);
            SpawnShip(center + new Vector3(0, 0, -30), team, ShipType.ShipOfTheLine, faction, fleet);
            SpawnShip(center + new Vector3(-30, 0, 20), team, ShipType.Frigate, faction, fleet);
            SpawnShip(center + new Vector3(-30, 0, -20), team, ShipType.Frigate, faction, fleet);
            SpawnShip(center + new Vector3(-50, 0, 0), team, ShipType.Brig, faction, fleet);
        }

        private void SpawnShip(Vector3 position, int team, ShipType type, FactionType faction, List<Ship> fleet)
        {
            GameObject shipGO = CreateShipVisual(type, faction);
            shipGO.transform.position = position;
            shipGO.transform.rotation = Quaternion.Euler(0, team == 0 ? 90 : -90, 0);

            Ship ship = shipGO.AddComponent<Ship>();
            ShipData data = ShipData.CreateDefault(type, faction);
            ship.Initialize(data, team);

            ship.OnSunk += OnShipSunk;
            ship.OnSurrendered += OnShipSurrendered;

            fleet.Add(ship);
        }

        private GameObject CreateShipVisual(ShipType type, FactionType faction)
        {
            GameObject ship = new GameObject($"Ship_{type}");

            // Hull
            GameObject hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.transform.SetParent(ship.transform);
            hull.name = "Hull";

            float length, width, height;
            switch (type)
            {
                case ShipType.ShipOfTheLine:
                    length = 15f; width = 4f; height = 3f;
                    break;
                case ShipType.Frigate:
                    length = 12f; width = 3f; height = 2.5f;
                    break;
                case ShipType.Brig:
                    length = 8f; width = 2.5f; height = 2f;
                    break;
                case ShipType.Sloop:
                    length = 6f; width = 2f; height = 1.5f;
                    break;
                default:
                    length = 10f; width = 3f; height = 2f;
                    break;
            }

            hull.transform.localScale = new Vector3(width, height, length);
            hull.transform.localPosition = new Vector3(0, height / 2f, 0);

            // Color based on faction
            var hullRenderer = hull.GetComponent<Renderer>();
            if (hullRenderer != null)
            {
                Color hullColor = faction == FactionType.Britain 
                    ? new Color(0.6f, 0.4f, 0.2f) // Brown
                    : new Color(0.3f, 0.3f, 0.5f); // Dark blue
                hullRenderer.material.color = hullColor;
            }

            // Mast
            GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.transform.SetParent(ship.transform);
            mast.name = "Mast";
            mast.transform.localScale = new Vector3(0.3f, height * 2f, 0.3f);
            mast.transform.localPosition = new Vector3(0, height * 2f, 0);

            var mastRenderer = mast.GetComponent<Renderer>();
            if (mastRenderer != null)
            {
                mastRenderer.material.color = new Color(0.4f, 0.3f, 0.2f);
            }

            // Sail
            GameObject sail = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sail.transform.SetParent(ship.transform);
            sail.name = "Sail";
            sail.transform.localScale = new Vector3(width * 1.5f, height * 1.5f, 1f);
            sail.transform.localPosition = new Vector3(0, height * 2.5f, 0);
            sail.transform.localRotation = Quaternion.Euler(0, 90, 0);

            var sailRenderer = sail.GetComponent<Renderer>();
            if (sailRenderer != null)
            {
                sailRenderer.material.color = Color.white;
            }

            // Remove colliders from visual parts
            foreach (var col in ship.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            // Add single collider to ship
            BoxCollider shipCol = ship.AddComponent<BoxCollider>();
            shipCol.size = new Vector3(width, height, length);
            shipCol.center = new Vector3(0, height / 2f, 0);

            return ship;
        }

        #endregion

        #region Wind

        private void UpdateWind()
        {
            windChangeTimer -= Time.deltaTime;

            if (windChangeTimer <= 0f)
            {
                // Gradually shift wind
                float windShift = Random.Range(-30f, 30f);
                windDirection = (windDirection + windShift + 360f) % 360f;

                float strengthChange = Random.Range(-0.3f, 0.3f);
                windStrength = Mathf.Clamp(windStrength + strengthChange, 0.3f, 2f);

                windChangeTimer = windChangeInterval + Random.Range(-10f, 10f);
                OnWindChanged?.Invoke();

                Debug.Log($"[NavalBattle] Wind shifted to {windDirection:F0}° at {windStrength:F1}");
            }
        }

        /// <summary>
        /// Get wind effect on ship speed based on heading.
        /// Returns 0.3 (against wind) to 1.5 (with wind)
        /// </summary>
        public float GetWindEffect(float shipHeading)
        {
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(shipHeading, windDirection));

            // Sailing with wind (0°) = 1.5x speed
            // Sailing across wind (90°) = 1.0x speed
            // Sailing against wind (180°) = 0.3x speed (can't sail directly into wind)

            float effect;
            if (angleDiff < 45f)
            {
                // Running with wind
                effect = Mathf.Lerp(1.5f, 1.2f, angleDiff / 45f);
            }
            else if (angleDiff < 135f)
            {
                // Beam reach / close hauled
                effect = Mathf.Lerp(1.2f, 0.8f, (angleDiff - 45f) / 90f);
            }
            else
            {
                // Into the wind
                effect = Mathf.Lerp(0.8f, 0.3f, (angleDiff - 135f) / 45f);
            }

            return effect * windStrength;
        }

        public Vector3 GetWindVector()
        {
            return Quaternion.Euler(0, windDirection, 0) * Vector3.forward * windStrength;
        }

        #endregion

        #region Input

        private void HandleInput()
        {
            // Selection
            if (Input.GetMouseButtonDown(0))
            {
                HandleSelection();
            }

            // Movement orders
            if (Input.GetMouseButtonDown(1) && selectedShips.Count > 0)
            {
                HandleMoveOrder();
            }

            // Fire orders
            if (Input.GetKeyDown(KeyCode.F))
            {
                HandleFireOrder(true); // Left broadside
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                HandleFireOrder(false); // Right broadside
            }

            // Speed control
            if (Input.GetKeyDown(KeyCode.W))
            {
                foreach (var ship in selectedShips)
                    ship.SetTargetSpeed(1f);
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                foreach (var ship in selectedShips)
                    ship.SetTargetSpeed(0f);
            }

            // Boarding
            if (Input.GetKeyDown(KeyCode.B))
            {
                HandleBoardingOrder();
            }
        }

        private void HandleSelection()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Ship ship = hit.collider.GetComponentInParent<Ship>();
                if (ship != null && ship.TeamId == 0)
                {
                    if (!Input.GetKey(KeyCode.LeftShift))
                    {
                        ClearSelection();
                    }

                    if (!selectedShips.Contains(ship))
                    {
                        selectedShips.Add(ship);
                        ship.SetSelected(true);
                    }
                }
                else if (!Input.GetKey(KeyCode.LeftShift))
                {
                    ClearSelection();
                }
            }
        }

        private void ClearSelection()
        {
            foreach (var ship in selectedShips)
            {
                ship.SetSelected(false);
            }
            selectedShips.Clear();
        }

        private void HandleMoveOrder()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane waterPlane = new Plane(Vector3.up, Vector3.zero);

            if (waterPlane.Raycast(ray, out float distance))
            {
                Vector3 targetPoint = ray.GetPoint(distance);

                foreach (var ship in selectedShips)
                {
                    // Calculate heading to target
                    Vector3 toTarget = targetPoint - ship.transform.position;
                    float targetHeading = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    
                    // Start turning towards target
                    StartCoroutine(NavigateShipTo(ship, targetPoint));
                }
            }
        }

        private System.Collections.IEnumerator NavigateShipTo(Ship ship, Vector3 target)
        {
            ship.SetTargetSpeed(1f);

            while (ship.IsOperational)
            {
                Vector3 toTarget = target - ship.transform.position;
                float distance = toTarget.magnitude;

                if (distance < 10f)
                {
                    ship.SetTargetSpeed(0f);
                    yield break;
                }

                float targetHeading = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                float headingDiff = Mathf.DeltaAngle(ship.Heading, targetHeading);

                if (Mathf.Abs(headingDiff) > 5f)
                {
                    ship.Turn(Mathf.Sign(headingDiff));
                }

                yield return null;
            }
        }

        private void HandleFireOrder(bool leftSide)
        {
            // Find closest enemy to fire at
            foreach (var ship in selectedShips)
            {
                Ship target = FindClosestEnemy(ship);
                if (target != null)
                {
                    ship.FireBroadside(leftSide, target);
                }
            }
        }

        private void HandleBoardingOrder()
        {
            foreach (var ship in selectedShips)
            {
                Ship target = FindClosestEnemy(ship);
                if (target != null && ship.CanBoard(target))
                {
                    ship.InitiateBoarding(target);
                }
            }
        }

        private Ship FindClosestEnemy(Ship fromShip)
        {
            Ship closest = null;
            float closestDist = float.MaxValue;

            var enemies = fromShip.TeamId == 0 ? enemyFleet : playerFleet;

            foreach (var enemy in enemies)
            {
                if (!enemy.IsOperational) continue;

                float dist = Vector3.Distance(fromShip.transform.position, enemy.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }

            return closest;
        }

        #endregion

        #region AI

        private void UpdateAI()
        {
            foreach (var ship in enemyFleet)
            {
                if (!ship.IsOperational) continue;

                // Simple AI: find closest player ship and engage
                Ship target = FindClosestEnemy(ship);
                if (target == null) continue;

                Vector3 toTarget = target.transform.position - ship.transform.position;
                float distance = toTarget.magnitude;

                // Navigate towards target
                float targetHeading = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                float headingDiff = Mathf.DeltaAngle(ship.Heading, targetHeading);

                // Try to get broadside angle
                float broadsideAngle = 90f;
                float idealHeading = targetHeading + (Random.value > 0.5f ? broadsideAngle : -broadsideAngle);
                headingDiff = Mathf.DeltaAngle(ship.Heading, idealHeading);

                if (Mathf.Abs(headingDiff) > 10f)
                {
                    ship.Turn(Mathf.Sign(headingDiff));
                }

                // Speed control
                if (distance > 150f)
                {
                    ship.SetTargetSpeed(1f);
                }
                else if (distance < 50f)
                {
                    ship.SetTargetSpeed(0.3f);
                }
                else
                {
                    ship.SetTargetSpeed(0.6f);
                }

                // Fire if in range and broadside
                if (distance < ship.Data.cannonRange)
                {
                    float angleToTarget = Vector3.SignedAngle(ship.transform.forward, toTarget, Vector3.up);
                    
                    if (angleToTarget > 60f && angleToTarget < 120f && ship.CanFireRight)
                    {
                        ship.FireBroadside(false, target);
                    }
                    else if (angleToTarget < -60f && angleToTarget > -120f && ship.CanFireLeft)
                    {
                        ship.FireBroadside(true, target);
                    }
                }
            }
        }

        #endregion

        #region Victory Conditions

        private void CheckVictoryConditions()
        {
            int playerOperational = 0;
            int enemyOperational = 0;

            foreach (var ship in playerFleet)
                if (ship.IsOperational) playerOperational++;

            foreach (var ship in enemyFleet)
                if (ship.IsOperational) enemyOperational++;

            if (enemyOperational == 0)
            {
                EndBattle(true); // Player wins
            }
            else if (playerOperational == 0)
            {
                EndBattle(false); // Enemy wins
            }
        }

        private void OnShipSunk(Ship ship)
        {
            Debug.Log($"[NavalBattle] {ship.Data.shipName} has sunk!");
        }

        private void OnShipSurrendered(Ship ship)
        {
            Debug.Log($"[NavalBattle] {ship.Data.shipName} has surrendered!");
        }

        #endregion

        #region Battle End

        public void EndBattle(bool playerVictory)
        {
            if (!battleActive) return;

            battleActive = false;
            OnBattleEnd?.Invoke();

            Debug.Log($"[NavalBattle] Battle ended! {(playerVictory ? "Victory!" : "Defeat!")}");

            // Return to campaign
            StartCoroutine(ReturnToCampaign(playerVictory));
        }

        private System.Collections.IEnumerator ReturnToCampaign(bool playerVictory)
        {
            yield return new WaitForSeconds(5f);

            if (NapoleonicWars.Core.GameManager.Instance != null)
            {
                NapoleonicWars.Core.GameManager.Instance.BattleResultVictory = playerVictory;
                NapoleonicWars.Core.GameManager.Instance.ReturnToCampaign();
            }
        }

        #endregion
    }
}
