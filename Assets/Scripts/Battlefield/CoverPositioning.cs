using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Core;
using NapoleonicWars.UI;
using NapoleonicWars.Units;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Handles the cover positioning mechanic - like Gate of Hell's cover system.
    /// Right-click + drag to assign units to take cover behind walls/buildings/trenches.
    /// Shows visual preview of unit placement before confirming.
    /// </summary>
    public class CoverPositioning : MonoBehaviour
    {
        public static CoverPositioning Instance { get; private set; }

        [Header("Input Settings")]
        [SerializeField] private KeyCode coverKey = KeyCode.C; // Press C + click to take cover
        [SerializeField] private int coverMouseButton = 1; // Right click

        [Header("Visual Settings")]
        [SerializeField] private Color coverPreviewColor = new Color(0.2f, 0.8f, 0.9f, 0.6f); // Cyan/blue

        [Header("Layer Settings")]
        [SerializeField] private LayerMask coverLayer; // Layer for cover objects

        private bool isSelectingCover = false;
        private Regiment selectedRegiment;
        private Vector3 targetCoverPosition;
        private Vector3 enemyDirection;
        private List<Vector3> previewPositions = new List<Vector3>();
        private bool isPreviewing = false;
        private TrenchComponent targetTrench; // Currently targeted trench

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Default cover layer if not set
            if (coverLayer == 0)
                coverLayer = LayerMask.GetMask("Default", "Cover");
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // Toggle cover mode with key
            if (Input.GetKeyDown(coverKey))
            {
                ToggleCoverMode();
            }

            // Exit cover mode with Escape
            if (isSelectingCover && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelCoverMode();
            }

            if (!isSelectingCover) return;

            // Get selected regiment
            if (SelectionManager.Instance != null && SelectionManager.Instance.SelectedRegiments.Count > 0)
            {
                selectedRegiment = SelectionManager.Instance.SelectedRegiments[0];
            }

            if (selectedRegiment == null) return;

            // Mouse down - start showing preview
            if (Input.GetMouseButtonDown(coverMouseButton))
            {
                StartCoverPlacement();
            }

            // Mouse held - update preview
            if (isPreviewing && Input.GetMouseButton(coverMouseButton))
            {
                UpdateCoverPreview();
            }

            // Mouse up - confirm placement
            if (isPreviewing && Input.GetMouseButtonUp(coverMouseButton))
            {
                ConfirmCoverPlacement();
            }
        }

        /// <summary>
        /// Toggle cover mode on/off
        /// </summary>
        public void ToggleCoverMode()
        {
            isSelectingCover = !isSelectingCover;
            
            if (isSelectingCover)
            {
                Debug.Log("[Cover] Mode activated - Right-click near walls/buildings to take cover");
                // Show UI feedback
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.LogPlayerEvent("Cover mode: Right-click near cover to position units");
            }
            else
            {
                CancelCoverMode();
            }
        }

        /// <summary>
        /// Cancel cover mode and clear preview
        /// </summary>
        public void CancelCoverMode()
        {
            isSelectingCover = false;
            isPreviewing = false;
            selectedRegiment = null;
            targetTrench = null;

            // Clear visual preview
            if (OrderFeedback.Instance != null)
                OrderFeedback.Instance.ClearFormationPreview();
        }

        /// <summary>
        /// Start cover placement - begin showing preview
        /// Check if clicking directly on a trench/cover object
        /// </summary>
        private void StartCoverPlacement()
        {
            if (selectedRegiment == null) return;

            // Raycast to check if clicking on a cover object (trench, wall, etc.)
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // First check for direct trench hit
            if (Physics.Raycast(ray, out hit, 500f, coverLayer))
            {
                // Check if we hit a cover/trench object
                TrenchComponent trench = hit.collider.GetComponent<TrenchComponent>();
                if (trench == null)
                    trench = hit.collider.GetComponentInParent<TrenchComponent>();

                if (trench != null && trench.HasSpace(selectedRegiment.GetAliveUnits().Count))
                {
                    // Direct trench click!
                    targetTrench = trench;
                    enemyDirection = trench.facingDirection;
                    targetCoverPosition = trench.transform.position;

                    // Calculate positions inside the trench
                    CalculateTrenchPositions(trench);

                    // Show preview immediately
                    ShowCoverPreview(previewPositions, trench.transform.position, enemyDirection);

                    isPreviewing = true;
                    Debug.Log("[Cover] Clicked on trench - placing units inside");
                    return;
                }

                // Check for cover tag
                if (hit.collider.CompareTag("Cover"))
                {
                    targetCoverPosition = hit.point;
                    enemyDirection = CalculateEnemyDirection(selectedRegiment);

                    // Calculate positions along the cover
                    int unitCount = selectedRegiment.GetAliveUnits().Count;
                    previewPositions = CoverSystem.Instance.GetCoverPositionsForRegiment(
                        hit.point, enemyDirection, unitCount);

                    ShowCoverPreview(previewPositions, hit.point, enemyDirection);

                    isPreviewing = true;
                    Debug.Log("[Cover] Clicked on cover object");
                    return;
                }
            }

            // Fallback: use mouse world position
            if (InputManager.Instance == null) return;

            Vector3 mousePos = InputManager.Instance.MouseWorldPosition;
            if (mousePos == Vector3.zero) return;

            // Calculate enemy direction
            enemyDirection = CalculateEnemyDirection(selectedRegiment);
            targetCoverPosition = mousePos;
            targetTrench = null;

            isPreviewing = true;
            Debug.Log("[Cover] No trench found, using ground position");
        }

        /// <summary>
        /// Calculate unit positions inside a specific trench
        /// </summary>
        private void CalculateTrenchPositions(TrenchComponent trench)
        {
            previewPositions.Clear();
            var aliveUnits = selectedRegiment.GetAliveUnits();
            int unitCount = aliveUnits.Count;

            for (int i = 0; i < unitCount; i++)
            {
                // Get firing position at the parapet
                Vector3 pos = trench.GetFiringPosition(i, unitCount);

                // Snap to terrain
                pos.y = BattleManager.GetTerrainHeight(pos);

                previewPositions.Add(pos);
            }
        }

        /// <summary>
        /// Update the cover placement preview while dragging
        /// </summary>
        private void UpdateCoverPreview()
        {
            if (selectedRegiment == null) return;

            // If we have a target trench, just update the preview positions
            if (targetTrench != null)
            {
                // Positions already calculated, just update visual
                ShowCoverPreview(previewPositions, targetTrench.transform.position, enemyDirection);
                return;
            }

            Vector3 mousePos = InputManager.Instance.MouseWorldPosition;
            if (mousePos == Vector3.zero) return;

            // Find best cover position near mouse
            Vector3 coverPos = CoverSystem.Instance.GetBestCoverPosition(
                mousePos, enemyDirection, 15f);

            if (coverPos == Vector3.zero)
            {
                // No good cover found - show warning or fallback position
                coverPos = mousePos;
            }

            targetCoverPosition = coverPos;

            // Calculate unit positions for preview
            int unitCount = selectedRegiment.GetAliveUnits().Count;
            previewPositions = CoverSystem.Instance.GetCoverPositionsForRegiment(
                coverPos, enemyDirection, unitCount);

            // Show visual preview
            ShowCoverPreview(previewPositions, coverPos, enemyDirection);
        }

        /// <summary>
        /// Confirm cover placement and move units
        /// </summary>
        private void ConfirmCoverPlacement()
        {
            if (selectedRegiment == null || previewPositions.Count == 0)
            {
                CancelCoverMode();
                return;
            }

            // If we targeted a trench, mark it as occupied
            if (targetTrench != null)
            {
                targetTrench.Occupy(selectedRegiment.GetAliveUnits().Count);
            }

            // Move each unit to its cover position
            var aliveUnits = selectedRegiment.GetAliveUnits();
            for (int i = 0; i < aliveUnits.Count && i < previewPositions.Count; i++)
            {
                aliveUnits[i].MoveTo(previewPositions[i]);
                aliveUnits[i].SetCoverStatus(true);

                // Rotate unit to face enemy direction (toward the trench edge)
                if (enemyDirection != Vector3.zero)
                {
                    aliveUnits[i].transform.rotation = Quaternion.LookRotation(enemyDirection, Vector3.up);
                }
            }

            // Log the action
            if (BattleLogUI.Instance != null)
            {
                if (targetTrench != null)
                    BattleLogUI.Instance.LogPlayerEvent($"{selectedRegiment.RegimentName} positioned in trench!");
                else
                    BattleLogUI.Instance.LogPlayerEvent($"{selectedRegiment.RegimentName} taking cover!");
            }

            // Clear preview
            if (OrderFeedback.Instance != null)
                OrderFeedback.Instance.ClearFormationPreview();

            isPreviewing = false;
            targetTrench = null;
        }

        /// <summary>
        /// Show visual preview of cover positions
        /// </summary>
        private void ShowCoverPreview(List<Vector3> positions, Vector3 coverCenter, Vector3 facingDir)
        {
            if (OrderFeedback.Instance == null || positions.Count == 0) return;

            // Calculate line positions for visual
            Vector3 lineDir = Vector3.Cross(Vector3.up, facingDir).normalized;
            Vector3 lineStart = positions[0];
            Vector3 lineEnd = positions[positions.Count - 1];

            // Show formation preview with cover color (cyan)
            Color coverColor = new Color(0.2f, 0.8f, 0.9f, 0.6f); // Cyan/blue for cover
            OrderFeedback.Instance.ShowFormationPreview(
                positions.ToArray(),
                lineStart,
                lineEnd,
                facingDir,
                coverColor
            );
        }

        /// <summary>
        /// Calculate the general enemy direction from a regiment
        /// </summary>
        private Vector3 CalculateEnemyDirection(Regiment regiment)
        {
            Vector3 avgEnemyPos = Vector3.zero;
            int enemyCount = 0;

            if (BattleManager.Instance != null)
            {
                var enemyRegiments = regiment.TeamId == 0 
                    ? BattleManager.Instance.EnemyRegiments 
                    : BattleManager.Instance.PlayerRegiments;

                foreach (var enemy in enemyRegiments)
                {
                    if (enemy != null && enemy.CachedAliveCount > 0)
                    {
                        avgEnemyPos += enemy.transform.position;
                        enemyCount++;
                    }
                }
            }

            if (enemyCount > 0)
            {
                avgEnemyPos /= enemyCount;
                Vector3 dir = (avgEnemyPos - regiment.transform.position).normalized;
                dir.y = 0f;
                return dir;
            }

            // Fallback: use regiment's forward direction
            return regiment.transform.forward;
        }

        /// <summary>
        /// Check if currently in cover mode
        /// </summary>
        public bool IsInCoverMode => isSelectingCover;
    }
}
