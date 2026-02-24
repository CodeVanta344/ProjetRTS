using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Units;

namespace NapoleonicWars.Core
{
    public class SpatialGrid : MonoBehaviour
    {
        public static SpatialGrid Instance { get; private set; }

        [Header("Grid Settings")]
        [SerializeField] private float cellSize = 20f;
        [SerializeField] private float updateInterval = 0.15f;

        private Dictionary<int, List<UnitBase>> grid = new Dictionary<int, List<UnitBase>>(256);
        private List<UnitBase> allUnits = new List<UnitBase>(4096);
        private HashSet<UnitBase> unitSet = new HashSet<UnitBase>();
        private float updateTimer;

        // Pooled result list to avoid GC allocations
        private List<UnitBase> sharedResult = new List<UnitBase>(128);

        // Cached inverse cell size
        private float invCellSize;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            invCellSize = 1f / cellSize;
        }

        private void Update()
        {
            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateInterval;
                RebuildGrid();
            }
        }

        public void RegisterUnit(UnitBase unit)
        {
            if (unitSet.Add(unit))
                allUnits.Add(unit);
        }

        public void UnregisterUnit(UnitBase unit)
        {
            unitSet.Remove(unit);
            // Don't remove from allUnits here — RebuildGrid handles cleanup
        }

        private void RebuildGrid()
        {
            // Clear all cells
            foreach (var kvp in grid)
                kvp.Value.Clear();

            // Re-insert alive units, remove dead/null entries entirely
            int writeIdx = 0;
            for (int i = 0; i < allUnits.Count; i++)
            {
                UnitBase unit = allUnits[i];
                if (unit == null || !unitSet.Contains(unit))
                    continue;

                if (unit.CurrentState == UnitState.Dead)
                {
                    unitSet.Remove(unit); // Remove from tracking — dead units will never revive
                    continue;
                }

                allUnits[writeIdx++] = unit;

                int key = GetCellKey(unit.transform.position);
                if (!grid.TryGetValue(key, out List<UnitBase> cell))
                {
                    cell = new List<UnitBase>(16);
                    grid[key] = cell;
                }
                cell.Add(unit);
            }
            // Trim removed entries
            if (writeIdx < allUnits.Count)
                allUnits.RemoveRange(writeIdx, allUnits.Count - writeIdx);
        }

        /// <summary>
        /// Returns nearby units. WARNING: the returned list is shared/pooled —
        /// do NOT cache it. Copy if needed.
        /// </summary>
        public List<UnitBase> GetNearbyUnits(Vector3 position, float radius)
        {
            sharedResult.Clear();

            int cellsToCheck = Mathf.CeilToInt(radius * invCellSize);
            int cx = (int)(position.x * invCellSize);
            int cz = (int)(position.z * invCellSize);
            if (position.x < 0) cx--;
            if (position.z < 0) cz--;

            float radiusSqr = radius * radius;

            for (int x = cx - cellsToCheck; x <= cx + cellsToCheck; x++)
            {
                for (int z = cz - cellsToCheck; z <= cz + cellsToCheck; z++)
                {
                    int key = x * 73856093 ^ z * 19349663;
                    if (!grid.TryGetValue(key, out List<UnitBase> cell)) continue;

                    for (int i = 0; i < cell.Count; i++)
                    {
                        UnitBase u = cell[i];
                        if (u == null) continue;

                        float dx = u.transform.position.x - position.x;
                        float dz = u.transform.position.z - position.z;
                        if (dx * dx + dz * dz <= radiusSqr)
                        {
                            sharedResult.Add(u);
                        }
                    }
                }
            }

            return sharedResult;
        }

        public UnitBase GetNearestEnemy(Vector3 position, int teamId, float maxRadius)
        {
            UnitBase nearest = null;
            float nearestDistSqr = maxRadius * maxRadius;

            int cellsToCheck = Mathf.CeilToInt(maxRadius * invCellSize);
            int cx = (int)(position.x * invCellSize);
            int cz = (int)(position.z * invCellSize);
            if (position.x < 0) cx--;
            if (position.z < 0) cz--;

            for (int x = cx - cellsToCheck; x <= cx + cellsToCheck; x++)
            {
                for (int z = cz - cellsToCheck; z <= cz + cellsToCheck; z++)
                {
                    int key = x * 73856093 ^ z * 19349663;
                    if (!grid.TryGetValue(key, out List<UnitBase> cell)) continue;

                    for (int i = 0; i < cell.Count; i++)
                    {
                        UnitBase u = cell[i];
                        if (u == null) continue;
                        if (u.TeamId == teamId) continue;
                        if (u.CurrentState == UnitState.Dead) continue;

                        float dx = u.transform.position.x - position.x;
                        float dz = u.transform.position.z - position.z;
                        float distSqr = dx * dx + dz * dz;

                        if (distSqr < nearestDistSqr)
                        {
                            nearestDistSqr = distSqr;
                            nearest = u;
                        }
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Returns all units in the grid. WARNING: the returned list is shared — 
        /// do NOT modify it. Read-only access only.
        /// </summary>
        public List<UnitBase> GetAllUnits()
        {
            return allUnits;
        }

        private int GetCellKey(Vector3 position)
        {
            int x = (int)(position.x * invCellSize);
            int z = (int)(position.z * invCellSize);
            if (position.x < 0) x--;
            if (position.z < 0) z--;
            return x * 73856093 ^ z * 19349663;
        }

        public int UnitCount => allUnits.Count;
    }
}
