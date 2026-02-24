using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Units;

namespace NapoleonicWars.Core
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        [Header("Selection Settings")]
        [SerializeField] private LayerMask selectableLayer;
        [SerializeField] private Color selectionBoxColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        [SerializeField] private Color selectionBoxBorderColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);

        private List<Regiment> selectedRegiments = new List<Regiment>();
        private List<UnitBase> selectedUnits = new List<UnitBase>();

        private bool isDragging;
        private Vector2 dragStartScreen;
        private Rect selectionRect;

        // Line extension (right-click drag to space out formation)
        private bool isExtendingLine = false;
        public bool IsExtendingLine => isExtendingLine;
        private bool isInFormationMode = false;
        private Vector3 lineExtendStartPos;
        private Regiment lineExtendingRegiment;
        private float originalUnitSpacing;

        public List<Regiment> SelectedRegiments => selectedRegiments;
        public List<UnitBase> SelectedUnits => selectedUnits;
        public bool HasSelection => selectedRegiments.Count > 0 || selectedUnits.Count > 0;

        private Camera mainCamera;
        private GameObject selectionBoxGO;
        private RectTransform selectionBoxRect;
        private Image selectionBoxImage;
        private Image selectionBoxBorder;

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
            CreateSelectionBoxUI();
        }

        private void CreateSelectionBoxUI()
        {
            // Find or create a Canvas
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("SelectionCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            // Selection box fill
            selectionBoxGO = new GameObject("SelectionBox");
            selectionBoxGO.transform.SetParent(canvas.transform, false);
            selectionBoxRect = selectionBoxGO.AddComponent<RectTransform>();
            selectionBoxImage = selectionBoxGO.AddComponent<Image>();
            selectionBoxImage.color = selectionBoxColor;
            selectionBoxImage.raycastTarget = false;

            // Border (outline via another Image)
            GameObject borderGO = new GameObject("SelectionBorder");
            borderGO.transform.SetParent(selectionBoxGO.transform, false);
            RectTransform borderRect = borderGO.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            selectionBoxBorder = borderGO.AddComponent<Image>();
            selectionBoxBorder.color = selectionBoxBorderColor;
            selectionBoxBorder.raycastTarget = false;
            // Make it a thin outline by using a sliced sprite or just overlay
            Outline outline = selectionBoxGO.AddComponent<Outline>();
            outline.effectColor = selectionBoxBorderColor;
            outline.effectDistance = new Vector2(2, 2);

            // Hide border fill (we only want the outline effect on the fill)
            Destroy(borderGO);

            selectionBoxGO.SetActive(false);
        }

        private void Update()
        {
            HandleSelection();
            HandleCommands();
            HandleFormationHotkeys();
            // Cancel formation mode with Escape
            if (Input.GetKeyDown(KeyCode.Escape) && isInFormationMode)
            {
                CancelFormationMode();
            }
        }

        private void HandleSelection()
        {
            if (InputManager.Instance == null) return;

            // Start drag
            if (InputManager.Instance.SelectionStarted)
            {
                isDragging = true;
                dragStartScreen = Input.mousePosition;
            }

            // End drag / click
            if (InputManager.Instance.SelectionReleased && isDragging)
            {
                isDragging = false;
                Vector2 dragEndScreen = Input.mousePosition;

                float dragDist = Vector2.Distance(dragStartScreen, dragEndScreen);

                if (dragDist < 5f)
                {
                    // Single click selection
                    HandleClickSelection();
                }
                else
                {
                    // Box selection
                    HandleBoxSelection(dragStartScreen, dragEndScreen);
                }
            }
        }

        private void HandleClickSelection()
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                UnitBase clickedUnit = hit.collider.GetComponent<UnitBase>();

                if (clickedUnit != null && clickedUnit.TeamId == 0)
                {
                    if (!InputManager.Instance.IsAdditive)
                        ClearSelection();

                    // Select the whole regiment if the unit belongs to one
                    if (clickedUnit.Regiment != null)
                    {
                        SelectRegiment(clickedUnit.Regiment);
                    }
                    else
                    {
                        SelectUnit(clickedUnit);
                    }
                }
                else if (!InputManager.Instance.IsAdditive)
                {
                    ClearSelection();
                }
            }
            else if (!InputManager.Instance.IsAdditive)
            {
                ClearSelection();
            }
        }

        private void HandleBoxSelection(Vector2 start, Vector2 end)
        {
            if (!InputManager.Instance.IsAdditive)
                ClearSelection();

            // Create screen-space rect
            float minX = Mathf.Min(start.x, end.x);
            float maxX = Mathf.Max(start.x, end.x);
            float minY = Mathf.Min(start.y, end.y);
            float maxY = Mathf.Max(start.y, end.y);

            selectionRect = new Rect(minX, minY, maxX - minX, maxY - minY);

            // Find all units within the box using spatial grid for better performance
            HashSet<Regiment> regimentsToSelect = new HashSet<Regiment>();
            
            System.Collections.Generic.IEnumerable<UnitBase> allUnits;
            if (SpatialGrid.Instance != null)
            {
                // Use spatial grid — returns shared list, zero allocation
                allUnits = SpatialGrid.Instance.GetAllUnits();
            }
            else
            {
                // Fallback to FindObjectsByType if spatial grid not available
                allUnits = FindObjectsByType<UnitBase>(FindObjectsSortMode.None);
            }

            foreach (var unit in allUnits)
            {
                if (unit.TeamId != 0) continue;
                if (unit.CurrentState == UnitState.Dead) continue;

                Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPos.z > 0 && selectionRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                {
                    if (unit.Regiment != null)
                    {
                        regimentsToSelect.Add(unit.Regiment);
                    }
                    else
                    {
                        SelectUnit(unit);
                    }
                }
            }

            foreach (var regiment in regimentsToSelect)
            {
                SelectRegiment(regiment);
            }
        }

        private void HandleCommands()
        {
            if (!HasSelection) return;
            if (InputManager.Instance == null) return;

            // Check if we're in deployment phase
            bool isDeployment = BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase;

            // Handle line extension (right-click drag) - works in both deployment and battle
            HandleLineExtension();

            // Don't process normal commands while extending line
            if (isExtendingLine) return;
            
            if (InputManager.Instance.CommandIssued)
            {
                if (mainCamera == null) return;

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    // Check if we clicked on an enemy
                    UnitBase clickedUnit = hit.collider.GetComponent<UnitBase>();

                    if (clickedUnit != null && clickedUnit.TeamId != 0 && clickedUnit.CurrentState != UnitState.Dead)
                    {
                        // Attack command - only allowed during battle, not deployment
                        if (!isDeployment)
                        {
                            IssueAttackCommand(clickedUnit);
                        }
                    }
                    else
                    {
                        // Move command - allowed in both deployment and battle
                        Vector3 destination = hit.point;
                        
                        // During deployment, restrict movement to deployment zone
                        if (isDeployment)
                        {
                            // Allow movement for player units (TeamId == 0)
                            if (selectedRegiments.Count > 0 && selectedRegiments[0].TeamId == 0)
                            {
                                // Check if destination is within deployment zone
                                if (!BattleManager.Instance.IsWithinDeploymentZone(destination, 0))
                                {
                                    Debug.Log("[SelectionManager] Cannot move outside deployment zone!");
                                    return;
                                }
                            }
                        }
                        
                        IssueMoveCommand(destination);
                    }
                }
                else
                {
                    // Move to ground plane position
                    Vector3 destination = InputManager.Instance.MouseWorldPosition;
                    
                    // During deployment, check zone restriction
                    if (isDeployment && selectedRegiments.Count > 0 && selectedRegiments[0].TeamId == 0)
                    {
                        if (!BattleManager.Instance.IsWithinDeploymentZone(destination, 0))
                        {
                            Debug.Log("[SelectionManager] Cannot move outside deployment zone!");
                            return;
                        }
                    }
                    
                    IssueMoveCommand(destination);
                }
            }
        }

        // Cached values for line extension preview
        private float previewSpacing;
        private float previewFacingAngle;
        private Vector3 previewDestination;

        /// <summary>
        /// Handle line extension: right-click and drag to define line width and facing
        /// Like Total War games - drag to set destination, facing direction, and line width
        /// Shows a preview without moving soldiers until confirmed.
        /// </summary>
        private void HandleLineExtension()
        {
            if (selectedRegiments.Count == 0) return;
            
            // Start line extension on right-click down
            if (Input.GetMouseButtonDown(1))
            {
                isExtendingLine = true;
                lineExtendStartPos = InputManager.Instance.MouseWorldPosition;
                lineExtendingRegiment = selectedRegiments[0];
                originalUnitSpacing = lineExtendingRegiment.UnitSpacing;
                Debug.Log($"[LineExt] START - Regiment: {lineExtendingRegiment.RegimentName}, Current spacing: {originalUnitSpacing}");
            }

            // While holding right-click, show preview WITHOUT moving soldiers
            if (isExtendingLine && Input.GetMouseButton(1))
            {
                Vector3 currentPos = InputManager.Instance.MouseWorldPosition;
                Vector3 dragVector = currentPos - lineExtendStartPos;
                float dragDistance = dragVector.magnitude;

                Debug.Log($"[LineExt] HOLD - Drag distance: {dragDistance:F2}");

                if (dragDistance > 2f)
                {
                    // Get ACTUAL alive unit count first
                    var aliveUnits = lineExtendingRegiment.GetAliveUnits();
                    int unitCount = aliveUnits.Count;
                    if (unitCount <= 0) return;
                    
                    // Proportional ranks: how many soldiers fit in the front rank
                    // based on drag width and a comfortable spacing (1.2 - 1.5m)
                    float idealSpacing = 1.3f; // shoulder-to-shoulder Napoleonic spacing
                    int unitsPerRank = Mathf.Clamp(Mathf.FloorToInt(dragDistance / idealSpacing) + 1, 1, unitCount);
                    int effectiveRanks = Mathf.CeilToInt((float)unitCount / unitsPerRank);
                    effectiveRanks = Mathf.Max(1, effectiveRanks);
                    
                    // Recalculate actual spacing to fill the drag width evenly
                    previewSpacing = unitsPerRank > 1 
                        ? dragDistance / (unitsPerRank - 1) 
                        : idealSpacing;
                    previewSpacing = Mathf.Clamp(previewSpacing, 0.8f, 5f);
                    
                    // Apply rank count
                    lineExtendingRegiment.SetRankCount(effectiveRanks);
                    
                    Debug.Log($"[LineExt] STRETCH - Units: {unitCount}, Ranks: {effectiveRanks}, PerRank: {unitsPerRank}, Spacing: {previewSpacing:F2}");
                    
                    // Apply the new spacing BEFORE changing formation to avoid double movement
                    lineExtendingRegiment.SetUnitSpacing(previewSpacing);
                    
                    // Force line formation type WITHOUT moving units (MoveRegiment will handle movement)
                    if (lineExtendingRegiment.CurrentFormation != FormationType.Line)
                    {
                        lineExtendingRegiment.SetFormationTypeOnly(FormationType.Line);
                    }
                    
                    // Calculate facing direction (perpendicular to drag direction)
                    // The mouse position relative to the LINE determines which side troops face
                    Vector3 lineDirection = dragVector.normalized;
                    Vector3 perpendicular = Vector3.Cross(lineDirection, Vector3.up);
                    
                    // Determine facing: which side of the drag line is the regiment currently on?
                    // Use the regiment center → midpoint of line, then check which side the center falls on
                    Vector3 lineMidpoint = (lineExtendStartPos + currentPos) * 0.5f;
                    Vector3 regimentCenter = lineExtendingRegiment.transform.position;
                    Vector3 centerToMidpoint = lineMidpoint - regimentCenter;
                    centerToMidpoint.y = 0f;
                    
                    // If regiment is "behind" the line (same side as perpendicular points), face the other way
                    // This means troops always face AWAY from their current position toward the enemy side
                    Vector3 facingDirection;
                    if (Vector3.Dot(centerToMidpoint, perpendicular) > 0)
                        facingDirection = perpendicular;
                    else
                        facingDirection = -perpendicular;
                    
                    previewFacingAngle = Mathf.Atan2(facingDirection.x, facingDirection.z) * Mathf.Rad2Deg;
                    
                    // ANCHOR AT LEFT: destination is at lineStart (left edge), not center
                    // Line goes from left (lineStart) to right (lineEnd)
                    Vector3 leftEdge = lineExtendStartPos;
                    Vector3 rightEdge = currentPos;
                    previewDestination = leftEdge; // Snap point is at LEFT edge
                    
                    // Calculate actual line width with clamped spacing
                    float actualLineWidth = previewSpacing * (unitsPerRank - 1);
                    
                    // Calculate line endpoints for visual (from left to right of formation)
                    Vector3 lineStart = leftEdge;
                    Vector3 lineEnd = leftEdge + lineDirection * actualLineWidth;
                    
                    // Calculate preview positions - anchored at left
                    // First unit at leftEdge, others extending to the right
                    Vector3[] previewPositions = new Vector3[unitCount];
                    Quaternion rotation = Quaternion.Euler(0f, previewFacingAngle, 0f);
                    float rowSpacing = lineExtendingRegiment.RowSpacing;
                    int rows = Mathf.Clamp(effectiveRanks, 1, 5);
                    
                    int index = 0;
                    for (int row = 0; row < rows && index < unitCount; row++)
                    {
                        for (int col = 0; col < unitsPerRank && index < unitCount; col++)
                        {
                            // Local position: x goes right from left edge, z goes BACKWARD for rows
                            // Row 0 = front rank (at the line), deeper rows go BEHIND
                            Vector3 localPos = new Vector3(
                                col * previewSpacing,
                                0f,
                                -row * rowSpacing
                            );
                            // Rotate and translate to world position
                            previewPositions[index] = leftEdge + rotation * localPos;
                            index++;
                        }
                    }
                    
                    // Show visual preview
                    if (NapoleonicWars.UI.OrderFeedback.Instance != null)
                    {
                        NapoleonicWars.UI.OrderFeedback.Instance.ShowFormationPreview(
                            previewPositions, lineStart, lineEnd, facingDirection);
                    }
                }
                else
                {
                    // Clear preview if drag is too short
                    if (NapoleonicWars.UI.OrderFeedback.Instance != null)
                    {
                        NapoleonicWars.UI.OrderFeedback.Instance.ClearFormationPreview();
                    }
                }
            }

            // Release right-click to confirm movement with new formation
            if (isExtendingLine && Input.GetMouseButtonUp(1))
            {
                Debug.Log("[LineExt] RELEASE");
                
                // Clear preview
                if (NapoleonicWars.UI.OrderFeedback.Instance != null)
                {
                    NapoleonicWars.UI.OrderFeedback.Instance.ClearFormationPreview();
                }

                Vector3 currentPos = InputManager.Instance.MouseWorldPosition;
                Vector3 dragVector = currentPos - lineExtendStartPos;
                float dragDistance = dragVector.magnitude;

                Debug.Log($"[LineExt] Final drag distance: {dragDistance:F2}");

                if (dragDistance > 2f)
                {
                    Debug.Log($"[LineExt] APPLY - Spacing already set to: {previewSpacing:F2}, Dest: {previewDestination}");
                    
                    // Spacing was already applied during preview phase
                    // Just move regiment to destination with new facing
                    lineExtendingRegiment.MoveRegiment(previewDestination, previewFacingAngle);
                    
                    // Visual feedback
                    if (NapoleonicWars.UI.OrderFeedback.Instance != null)
                    {
                        NapoleonicWars.UI.OrderFeedback.Instance.ShowMoveMarker(previewDestination);
                    }
                }
                else
                {
                    Debug.Log("[LineExt] Drag too short - will process as normal move");
                }
                // Short drag = let HandleCommands deal with it as a simple move

                isExtendingLine = false;
                lineExtendingRegiment = null;
            }
        }

        private void IssueMoveCommand(Vector3 destination)
        {
            // Check deployment boundary during deployment phase
            if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase)
            {
                if (DeploymentBoundary.Instance != null)
                {
                    // Check each selected regiment
                    foreach (var regiment in selectedRegiments)
                    {
                        int teamId = regiment.TeamId;
                        if (!DeploymentBoundary.Instance.IsValidDeploymentPosition(destination, teamId))
                        {
                            // Clamp destination to valid zone
                            destination = DeploymentBoundary.Instance.GetClosestValidPosition(destination, teamId);
                            Debug.Log($"[SelectionManager] Move order clamped to deployment zone");
                        }
                    }
                }
            }

            if (selectedRegiments.Count > 0)
            {
                // Spread regiments in a line at destination
                float spacing = 15f;
                float startOffset = -(selectedRegiments.Count - 1) * spacing * 0.5f;

                for (int i = 0; i < selectedRegiments.Count; i++)
                {
                    Vector3 regDest = destination + Vector3.right * (startOffset + i * spacing);
                    
                    // Check boundary for each regiment position
                    if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase
                        && DeploymentBoundary.Instance != null)
                    {
                        regDest = DeploymentBoundary.Instance.GetClosestValidPosition(regDest, selectedRegiments[i].TeamId);
                    }
                    
                    selectedRegiments[i].MoveRegiment(regDest);
                }
            }

            foreach (var unit in selectedUnits)
            {
                Vector3 unitDest = destination;
                
                // Check boundary for individual units too
                if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase
                    && DeploymentBoundary.Instance != null && unit.Regiment != null)
                {
                    unitDest = DeploymentBoundary.Instance.GetClosestValidPosition(unitDest, unit.Regiment.TeamId);
                }
                
                unit.MoveTo(unitDest);
            }

            // Visual feedback
            if (NapoleonicWars.UI.OrderFeedback.Instance != null)
                NapoleonicWars.UI.OrderFeedback.Instance.ShowMoveMarker(destination);
        }

        private void IssueAttackCommand(UnitBase target)
        {
            if (target.Regiment != null)
            {
                foreach (var regiment in selectedRegiments)
                {
                    regiment.AttackTargetRegiment(target.Regiment);
                }
            }

            foreach (var unit in selectedUnits)
            {
                unit.AttackTarget(target);
            }

            // Visual feedback - show attack order arrow
            if (NapoleonicWars.UI.OrderFeedback.Instance != null)
            {
                // Get source position (from selected regiment or unit)
                Vector3 fromPos = selectedRegiments.Count > 0 
                    ? selectedRegiments[0].transform.position 
                    : (selectedUnits.Count > 0 ? selectedUnits[0].transform.position : Vector3.zero);
                
                // Show attack arrow pointing to target
                string targetName = target.Regiment != null ? target.Regiment.RegimentName : "Enemy";
                NapoleonicWars.UI.OrderFeedback.Instance.ShowAttackOrderArrow(
                    fromPos, target.transform.position, targetName);
                
                // Also show marker at target position
                NapoleonicWars.UI.OrderFeedback.Instance.ShowAttackMarker(target.transform.position);
            }
        }

        private void HandleFormationHotkeys()
        {
            if (!HasSelection) return;
            if (InputManager.Instance == null) return;

            FormationType? newFormation = null;

            if (InputManager.Instance.FormationLine) newFormation = FormationType.Line;
            if (InputManager.Instance.FormationColumn) newFormation = FormationType.Column;
            if (InputManager.Instance.FormationSquare) newFormation = FormationType.Square;
            if (InputManager.Instance.FormationSkirmish) newFormation = FormationType.Skirmish;
            if (InputManager.Instance.FormationWedge) newFormation = FormationType.Wedge;
            if (InputManager.Instance.FormationOblique) newFormation = FormationType.Oblique;
            if (InputManager.Instance.FormationMixed) newFormation = FormationType.MixedOrder;

            if (newFormation.HasValue)
            {
                foreach (var regiment in selectedRegiments)
                {
                    regiment.SetFormation(newFormation.Value);
                }
            }

            // Increase rank count (Page Up)
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                foreach (var regiment in selectedRegiments)
                {
                    int newRanks = regiment.CurrentRankCount + 1;
                    regiment.SetRankCount(newRanks);
                    Debug.Log($"[Formation] {regiment.RegimentName}: {newRanks} ranks");
                }
            }

            // Decrease rank count (Page Down)
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                foreach (var regiment in selectedRegiments)
                {
                    int newRanks = regiment.CurrentRankCount - 1;
                    regiment.SetRankCount(newRanks);
                    Debug.Log($"[Formation] {regiment.RegimentName}: {newRanks} ranks");
                }
            }

            // Volley fire toggle (V)
            if (InputManager.Instance.VolleyToggle)
            {
                foreach (var regiment in selectedRegiments)
                {
                    regiment.ToggleVolleyFire();
                }
            }

            // Charge command (C) - charges nearest enemy regiment
            if (InputManager.Instance.ChargePressed)
            {
                IssueChargeCommand();
            }
        }

        private void IssueChargeCommand()
        {
            if (BattleManager.Instance == null) return;
            
            // Cannot charge during deployment phase
            if (BattleManager.Instance.IsDeploymentPhase)
            {
                Debug.Log("[SelectionManager] Cannot charge during deployment phase!");
                return;
            }

            foreach (var regiment in selectedRegiments)
            {
                if (regiment.UnitData == null || !regiment.UnitData.canCharge) continue;

                // Find nearest enemy regiment
                Regiment nearestEnemy = null;
                float nearestDist = float.MaxValue;

                foreach (var enemyReg in BattleManager.Instance.EnemyRegiments)
                {
                    if (enemyReg.CachedAliveCount <= 0) continue;
                    float dist = Vector3.Distance(regiment.transform.position, enemyReg.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestEnemy = enemyReg;
                    }
                }

                if (nearestEnemy != null)
                {
                    regiment.ChargeTargetRegiment(nearestEnemy);
                }
            }
        }

        public void SelectRegiment(Regiment regiment)
        {
            if (!selectedRegiments.Contains(regiment))
            {
                selectedRegiments.Add(regiment);
                regiment.SetSelected(true);
            }
        }

        public void SelectUnit(UnitBase unit)
        {
            if (!selectedUnits.Contains(unit))
            {
                selectedUnits.Add(unit);
                unit.SetSelected(true);
            }
        }

        public void ClearSelection()
        {
            foreach (var regiment in selectedRegiments)
                regiment.SetSelected(false);
            foreach (var unit in selectedUnits)
                unit.SetSelected(false);

            selectedRegiments.Clear();
            selectedUnits.Clear();
            
            // Clear formation preview when selection is cleared
            if (NapoleonicWars.UI.OrderFeedback.Instance != null)
            {
                NapoleonicWars.UI.OrderFeedback.Instance.ClearFormationPreview();
            }
        }

        /// <summary>
        /// Cancel formation mode and reset formation changes
        /// </summary>
        private void CancelFormationMode()
        {
            if (isExtendingLine && lineExtendingRegiment != null)
            {
                // Reset to original spacing
                lineExtendingRegiment.SetUnitSpacing(originalUnitSpacing);
                lineExtendingRegiment.SetRankCount(0); // Reset to auto-calculated
            }
            
            isExtendingLine = false;
            isInFormationMode = false;
            lineExtendingRegiment = null;
            
            // Clear any preview
            if (NapoleonicWars.UI.OrderFeedback.Instance != null)
            {
                NapoleonicWars.UI.OrderFeedback.Instance.ClearFormationPreview();
            }
            
            Debug.Log("[SelectionManager] Formation mode cancelled");
        }

        private void LateUpdate()
        {
            UpdateSelectionBoxUI();
        }

        private void UpdateSelectionBoxUI()
        {
            if (selectionBoxGO == null) return;

            if (!isDragging)
            {
                selectionBoxGO.SetActive(false);
                return;
            }

            selectionBoxGO.SetActive(true);

            Vector2 currentMouse = Input.mousePosition;
            float minX = Mathf.Min(dragStartScreen.x, currentMouse.x);
            float maxX = Mathf.Max(dragStartScreen.x, currentMouse.x);
            float minY = Mathf.Min(dragStartScreen.y, currentMouse.y);
            float maxY = Mathf.Max(dragStartScreen.y, currentMouse.y);

            float width = maxX - minX;
            float height = maxY - minY;

            selectionBoxRect.anchorMin = Vector2.zero;
            selectionBoxRect.anchorMax = Vector2.zero;
            selectionBoxRect.pivot = new Vector2(0f, 0f);
            selectionBoxRect.anchoredPosition = new Vector2(minX, minY);
            selectionBoxRect.sizeDelta = new Vector2(width, height);
        }
    }
}
