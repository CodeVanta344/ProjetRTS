using UnityEngine;
using System.Collections.Generic;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Manages cover detection and bonus calculation for units.
    /// Units near walls, buildings, or trenches get damage reduction.
    /// </summary>
    public class CoverSystem : MonoBehaviour
    {
        public static CoverSystem Instance { get; private set; }

        [Header("Cover Settings")]
        [SerializeField] private float coverDetectionRadius = 8f;
        [SerializeField] private float coverBonusPerMeter = 0.15f; // 15% damage reduction per meter of cover
        [SerializeField] private float maxCoverBonus = 0.6f; // Max 60% damage reduction
        
        // Cover object layers/tags
        [SerializeField] private LayerMask coverLayers;
        
        // Cached cover positions for quick lookup
        private List<CoverPoint> coverPoints = new List<CoverPoint>();
        private float updateTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Default cover layers if not set
            if (coverLayers == 0)
                coverLayers = LayerMask.GetMask("Default", "Obstacle", "Cover");
        }

        private void Update()
        {
            // Periodic update of cover points
            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = 2f; // Update every 2 seconds
                UpdateCoverPoints();
            }
        }

        /// <summary>
        /// Scan the battlefield for cover objects and cache their positions
        /// </summary>
        private void UpdateCoverPoints()
        {
            coverPoints.Clear();
            
            // Find all potential cover objects (walls, buildings, trenches, etc.)
            var colliders = Physics.OverlapSphere(Vector3.zero, 1000f, coverLayers);
            foreach (var col in colliders)
            {
                // Check if object is a cover type
                CoverType type = GetCoverType(col.gameObject);
                if (type != CoverType.None)
                {
                    coverPoints.Add(new CoverPoint
                    {
                        position = col.transform.position,
                        type = type,
                        height = GetCoverHeight(type),
                        gameObject = col.gameObject
                    });
                }
            }
        }

        /// <summary>
        /// Calculate cover bonus for a unit at given position
        /// Returns value between 0 and maxCoverBonus (damage reduction percentage)
        /// </summary>
        public float GetCoverBonus(Vector3 position, Vector3 attackerDirection)
        {
            float bestCover = 0f;
            
            foreach (var cover in coverPoints)
            {
                float distance = Vector3.Distance(position, cover.position);
                if (distance > coverDetectionRadius) continue;
                
                // Check if cover is between unit and attacker (facing the enemy)
                Vector3 toCover = (cover.position - position).normalized;
                float facingDot = Vector3.Dot(toCover, attackerDirection);
                
                // Only count cover if it's facing the enemy (facingDot > 0 means cover is in front)
                if (facingDot > 0.2f)
                {
                    float coverValue = cover.height / distance * coverBonusPerMeter;
                    bestCover = Mathf.Max(bestCover, coverValue);
                }
            }
            
            return Mathf.Clamp(bestCover, 0f, maxCoverBonus);
        }

        /// <summary>
        /// Get the best available cover position near the given position
        /// Returns Vector3.zero if no good cover found
        /// </summary>
        public Vector3 GetBestCoverPosition(Vector3 position, Vector3 enemyDirection, float searchRadius = 15f)
        {
            Vector3 bestPosition = Vector3.zero;
            float bestCoverValue = 0f;
            
            // Search in a grid pattern around the position
            int gridSteps = 5;
            float stepSize = searchRadius / gridSteps;
            
            for (int x = -gridSteps; x <= gridSteps; x++)
            {
                for (int z = -gridSteps; z <= gridSteps; z++)
                {
                    Vector3 testPos = position + new Vector3(x * stepSize, 0f, z * stepSize);
                    
                    // Check if position is valid (on terrain, not blocked)
                    if (!IsValidCoverPosition(testPos)) continue;
                    
                    // Calculate cover value at this position
                    float coverValue = GetCoverBonus(testPos, enemyDirection);
                    
                    // Prefer positions closer to the original position
                    float distancePenalty = Vector3.Distance(testPos, position) * 0.05f;
                    coverValue -= distancePenalty;
                    
                    if (coverValue > bestCoverValue)
                    {
                        bestCoverValue = coverValue;
                        bestPosition = testPos;
                    }
                }
            }
            
            return bestPosition;
        }

        /// <summary>
        /// Get multiple cover positions for a regiment - aligned in a single line along the cover
        /// </summary>
        public List<Vector3> GetCoverPositionsForRegiment(Vector3 center, Vector3 enemyDirection, int unitCount, float spacing = 1.2f)
        {
            List<Vector3> positions = new List<Vector3>();
            
            // Find the best cover position near center
            Vector3 bestCoverPos = GetBestCoverPosition(center, enemyDirection, 15f);
            if (bestCoverPos == Vector3.zero)
            {
                // Fallback: use center position
                bestCoverPos = center;
            }
            
            // Find the nearest cover object to determine the wall direction
            CoverPoint coverPoint = FindNearestCoverPoint(bestCoverPos);
            
            // Calculate line direction PERPENDICULAR to enemy (parallel to wall/cover)
            // Units should form a line SIDE BY SIDE, facing the enemy
            Vector3 lineDir = Vector3.Cross(Vector3.up, enemyDirection).normalized;
            
            // Position is slightly BEHIND the cover point (offset from cover toward safety)
            Vector3 coverOffset = -enemyDirection * 1.5f; // 1.5m behind cover
            Vector3 lineCenter = bestCoverPos + coverOffset;
            
            // Place units in a single line, evenly distributed
            float totalWidth = (unitCount - 1) * spacing;
            float startX = -totalWidth * 0.5f;
            
            for (int i = 0; i < unitCount; i++)
            {
                // Calculate position along the line
                float xOffset = startX + i * spacing;
                Vector3 pos = lineCenter + lineDir * xOffset;
                
                // Snap to terrain
                pos.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(pos);
                
                // Validate and add
                if (IsValidCoverPosition(pos))
                {
                    positions.Add(pos);
                }
                else
                {
                    // Find nearby valid position
                    Vector3 validPos = FindNearbyValidPosition(pos, 3f);
                    if (validPos != Vector3.zero)
                    {
                        validPos.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(validPos);
                        positions.Add(validPos);
                    }
                }
            }
            
            return positions;
        }

        /// <summary>
        /// Check if position is a valid cover spot (on terrain, walkable)
        /// </summary>
        private bool IsValidCoverPosition(Vector3 position)
        {
            // Check terrain height
            float terrainHeight = NapoleonicWars.Core.BattleManager.GetTerrainHeight(position);
            if (Mathf.Abs(position.y - terrainHeight) > 2f) return false;
            
            // Check if position is blocked by other objects
            Collider[] hits = Physics.OverlapSphere(position, 0.5f);
            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
                    return false;
            }
            
            return true;
        }

        /// <summary>
        /// Check if position is near any cover object
        /// </summary>
        private bool IsNearCover(Vector3 position, float radius)
        {
            foreach (var cover in coverPoints)
            {
                if (Vector3.Distance(position, cover.position) < radius)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Find nearest valid position to the given position
        /// </summary>
        private Vector3 FindNearbyValidPosition(Vector3 position, float searchRadius)
        {
            for (float r = 1f; r <= searchRadius; r += 1f)
            {
                for (int angle = 0; angle < 360; angle += 45)
                {
                    Vector3 testPos = position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * r;
                    testPos.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(testPos);
                    
                    if (IsValidCoverPosition(testPos))
                        return testPos;
                }
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Find the nearest cover point to a position
        /// </summary>
        private CoverPoint FindNearestCoverPoint(Vector3 position)
        {
            CoverPoint nearest = new CoverPoint();
            float minDist = float.MaxValue;
            
            foreach (var cover in coverPoints)
            {
                float dist = Vector3.Distance(position, cover.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = cover;
                }
            }
            
            return nearest;
        }

        /// <summary>
        /// Determine cover type based on game object name/tags
        /// </summary>
        private CoverType GetCoverType(GameObject obj)
        {
            string name = obj.name.ToLower();
            
            if (name.Contains("wall") || name.Contains("palisade") || name.Contains("fence"))
                return CoverType.Wall;
            if (name.Contains("trench") || name.Contains("redoubt") || name.Contains("parapet"))
                return CoverType.Trench;
            if (name.Contains("building") || name.Contains("house") || name.Contains("barn") || name.Contains("church"))
                return CoverType.Building;
            if (name.Contains("rock") || name.Contains("tree") || name.Contains("hay"))
                return CoverType.Natural;
            if (name.Contains("ruin") || name.Contains("rubble"))
                return CoverType.Ruin;
            
            // Check collider height - tall objects can be cover
            var col = obj.GetComponent<Collider>();
            if (col != null)
            {
                float height = col.bounds.size.y;
                if (height > 1f && height < 4f)
                    return CoverType.Wall;
                if (height >= 4f)
                    return CoverType.Building;
            }
            
            return CoverType.None;
        }

        private float GetCoverHeight(CoverType type)
        {
            switch (type)
            {
                case CoverType.Trench: return 1.5f;
                case CoverType.Wall: return 1.2f;
                case CoverType.Building: return 3f;
                case CoverType.Natural: return 1f;
                case CoverType.Ruin: return 0.8f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Debug visualization of cover points
        /// </summary>
        private void OnDrawGizmos()
        {
            if (coverPoints == null) return;
            
            Gizmos.color = Color.cyan;
            foreach (var cover in coverPoints)
            {
                Gizmos.DrawWireSphere(cover.position, cover.height);
            }
        }
    }

    public enum CoverType
    {
        None,
        Trench,     // Best cover - can crouch behind parapet
        Wall,       // Good cover - stone/wood walls
        Building,   // Excellent cover but exposed if attacked
        Natural,    // Trees, rocks, haystacks
        Ruin        // Partial cover from rubble
    }

    public struct CoverPoint
    {
        public Vector3 position;
        public CoverType type;
        public float height;
        public GameObject gameObject;
    }
}
