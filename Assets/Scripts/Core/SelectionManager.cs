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

        // Line extension logic removed as per user request

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
            HandleFormationHotkeys();
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


            // Issue commands on right-click RELEASE (not down) so units don't move
            // until the player has finished aiming. Short clicks (no line extension)
            // fall through here on GetMouseButtonUp.
            if (Input.GetMouseButtonUp(1))
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

        // HandleLineExtension removed

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
                    regiment.ApplyFormation(); // Apply immediately for standalone rank changes
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
                    regiment.ApplyFormation(); // Apply immediately for standalone rank changes
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

        // CancelFormationMode removed

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
