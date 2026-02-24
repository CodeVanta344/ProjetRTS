using UnityEngine;
using System.Collections.Generic;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Manages the deployment phase boundary between player and enemy zones.
    /// Before battle starts, units cannot cross to the enemy side.
    /// </summary>
    public class DeploymentBoundary : MonoBehaviour
    {
        public static DeploymentBoundary Instance { get; private set; }

        [Header("Boundary Settings")]
        [SerializeField] private float boundaryXPosition = 0f; // X coordinate of the dividing line
        [SerializeField] private Color playerZoneColor = new Color(0.2f, 0.6f, 0.9f, 0.3f); // Blue for player
        [SerializeField] private Color enemyZoneColor = new Color(0.9f, 0.3f, 0.2f, 0.3f); // Red for enemy

        [Header("Visual Line")]
        [SerializeField] private float lineWidth = 0.5f;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.6f);

        private LineRenderer boundaryLine;
        private GameObject playerZoneVisual;
        private GameObject enemyZoneVisual;
        private Terrain terrain;

        public float BoundaryX => boundaryXPosition;
        public bool IsBattleStarted => BattleManager.Instance != null && BattleManager.Instance.IsBattleActive;

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
            terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                // Calculate center of terrain
                Vector3 terrainSize = terrain.terrainData.size;
                boundaryXPosition = terrain.transform.position.x + terrainSize.x * 0.5f;
            }

            CreateVisualBoundary();
        }

        /// <summary>
        /// Check if a position is valid for the given team during deployment
        /// </summary>
        public bool IsValidDeploymentPosition(Vector3 position, int teamId)
        {
            // If battle has started, all positions are valid
            if (IsBattleStarted) return true;

            // Team 0 (Player) must stay on left side (x < boundary)
            // Team 1 (Enemy) must stay on right side (x > boundary)
            if (teamId == 0)
                return position.x < boundaryXPosition;
            else
                return position.x > boundaryXPosition;
        }

        /// <summary>
        /// Clamp a position to the valid deployment zone for a team
        /// </summary>
        public Vector3 ClampToDeploymentZone(Vector3 position, int teamId)
        {
            if (IsBattleStarted) return position;

            float clampedX = position.x;
            float buffer = 5f; // Stay 5 units away from boundary

            if (teamId == 0)
            {
                // Player - must be left of boundary
                clampedX = Mathf.Min(position.x, boundaryXPosition - buffer);
            }
            else
            {
                // Enemy - must be right of boundary
                clampedX = Mathf.Max(position.x, boundaryXPosition + buffer);
            }

            return new Vector3(clampedX, position.y, position.z);
        }

        /// <summary>
        /// Get the closest valid deployment position for a team
        /// </summary>
        public Vector3 GetClosestValidPosition(Vector3 desiredPosition, int teamId)
        {
            if (IsBattleStarted) return desiredPosition;

            Vector3 clamped = ClampToDeploymentZone(desiredPosition, teamId);
            clamped.y = terrain != null ? terrain.SampleHeight(clamped) : desiredPosition.y;
            return clamped;
        }

        /// <summary>
        /// Start the battle - removes deployment restrictions
        /// </summary>
        public void StartBattle()
        {
            HideBoundaryVisuals();
            Debug.Log("[DeploymentBoundary] Battle started - deployment restrictions lifted");
        }

        private void CreateVisualBoundary()
        {
            if (terrain == null) return;

            Vector3 terrainSize = terrain.terrainData.size;
            Vector3 terrainPos = terrain.transform.position;
            float terrainZ = terrainPos.z + terrainSize.z;

            // Create boundary line
            GameObject lineObj = new GameObject("DeploymentBoundaryLine");
            lineObj.transform.SetParent(transform);
            boundaryLine = lineObj.AddComponent<LineRenderer>();
            boundaryLine.material = new Material(Shader.Find("Sprites/Default"));
            boundaryLine.startColor = lineColor;
            boundaryLine.endColor = lineColor;
            boundaryLine.startWidth = lineWidth;
            boundaryLine.endWidth = lineWidth;
            boundaryLine.positionCount = 2;
            boundaryLine.useWorldSpace = true;

            // Draw line from terrain start to end in Z
            Vector3 startPos = new Vector3(boundaryXPosition, 0.1f, terrainPos.z);
            Vector3 endPos = new Vector3(boundaryXPosition, 0.1f, terrainZ);
            
            if (terrain != null)
            {
                startPos.y = terrain.SampleHeight(startPos) + 0.5f;
                endPos.y = terrain.SampleHeight(endPos) + 0.5f;
            }

            boundaryLine.SetPosition(0, startPos);
            boundaryLine.SetPosition(1, endPos);

            // Create zone visuals (semi-transparent planes)
            CreateZoneVisuals(terrainPos, terrainSize);
        }

        private void CreateZoneVisuals(Vector3 terrainPos, Vector3 terrainSize)
        {
            // Player zone (left side)
            playerZoneVisual = CreateZonePlane(
                "PlayerDeploymentZone",
                terrainPos.x,
                boundaryXPosition - 2f, // Stop 2 units before boundary
                terrainPos.z,
                terrainSize.z,
                playerZoneColor
            );

            // Enemy zone (right side)
            enemyZoneVisual = CreateZonePlane(
                "EnemyDeploymentZone",
                boundaryXPosition + 2f, // Start 2 units after boundary
                terrainPos.x + terrainSize.x,
                terrainPos.z,
                terrainSize.z,
                enemyZoneColor
            );
        }

        private GameObject CreateZonePlane(string name, float minX, float maxX, float minZ, float sizeZ, Color color)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            plane.transform.SetParent(transform);

            float width = maxX - minX;
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = minZ + sizeZ * 0.5f;

            plane.transform.position = new Vector3(centerX, 0.1f, centerZ);
            plane.transform.localScale = new Vector3(width * 0.1f, 1f, sizeZ * 0.1f);

            // Remove collider
            Destroy(plane.GetComponent<Collider>());

            // Set material
            Renderer rend = plane.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha blend
            rend.material = mat;

            return plane;
        }

        private void HideBoundaryVisuals()
        {
            if (boundaryLine != null)
                boundaryLine.gameObject.SetActive(false);
            if (playerZoneVisual != null)
                playerZoneVisual.SetActive(false);
            if (enemyZoneVisual != null)
                enemyZoneVisual.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            if (terrain == null) return;

            // Draw boundary line in editor
            Gizmos.color = Color.white;
            Vector3 terrainSize = terrain.terrainData.size;
            Vector3 terrainPos = terrain.transform.position;

            Vector3 start = new Vector3(boundaryXPosition, terrainPos.y + 1f, terrainPos.z);
            Vector3 end = new Vector3(boundaryXPosition, terrainPos.y + 1f, terrainPos.z + terrainSize.z);

            Gizmos.DrawLine(start, end);

            // Draw labels
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(start + Vector3.left * 10f, "PLAYER ZONE");
            UnityEditor.Handles.Label(start + Vector3.right * 10f, "ENEMY ZONE");
            #endif
        }
    }
}
