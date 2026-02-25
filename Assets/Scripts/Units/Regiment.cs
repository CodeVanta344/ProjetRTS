using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Core;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    public enum FormationType
    {
        Line,
        Column,
        Square,
        Skirmish,
        Wedge,       // Cavalry V-formation (charge)
        Oblique,     // Diagonal attack line (Frederick/Napoleon)
        MixedOrder   // French Ordre Mixte (center column + flanking lines)
    }

    public class Regiment : MonoBehaviour
    {
        [Header("Regiment Info")]
        [SerializeField] private string regimentName = "1st Regiment";
        [SerializeField] private UnitData unitData;
        [SerializeField] private FormationType currentFormation = FormationType.Line;

        [Header("Formation Settings")]
        [SerializeField] private float unitSpacing = 1.2f;
        [SerializeField] private float rowSpacing = 1.5f;
        [SerializeField] private int desiredRanks = 0; // 0 = auto-compute based on unit count

        [Header("Volley Fire")]
        private int currentFiringRank = 0; // 0 = front rank, 1 = middle, 2 = back
        private float volleyCooldown = 0f;

        [Header("Regiment State")]
        private bool isVolleyMode;
        private bool isSelected;

        // Firing range visualizer - one per regiment (not per soldier)
        private FiringRangeVisualizer rangeVisualizer;

        private List<UnitBase> units = new List<UnitBase>();

        // Cached alive count — updated periodically to avoid per-frame iteration
        private int cachedAliveCount;
        private float aliveCountTimer;

        // Cached aggregate stats — updated alongside alive count
        private float cachedAverageMorale;
        private float cachedAverageStamina;
        private int cachedTotalAmmo;

        // Pooled alive list to avoid GC allocations
        private List<UnitBase> pooledAliveList = new List<UnitBase>(64);

        // Pooled formation positions array to avoid GC allocations
        private Vector3[] pooledFormationPositions = new Vector3[128];

        // Pooled array for nearest-slot assignment (avoids GC)
        private int[] pooledSlotAssignment = new int[128];
        private bool[] pooledSlotUsed = new bool[128];
        private Vector3[] pooledFormationWorldPos = new Vector3[128];

        // Public accessors
        public string RegimentName => regimentName;
        public UnitData UnitData => unitData;
        public FormationType CurrentFormation => currentFormation;
        public float UnitSpacing => unitSpacing;
        public float RowSpacing => rowSpacing;
        public List<UnitBase> Units => units;
        public bool IsSelected => isSelected;
        public bool IsVolleyMode => isVolleyMode;
        public int TeamId { get; set; } = 0;
        public bool OfficerAlive { get; private set; } = true;
        
        /// <summary>
        /// The regiment's intended facing direction (world-space).
        /// Units should face this direction while moving in formation.
        /// This is separate from transform.forward to avoid parent rotation dragging children.
        /// </summary>
        public Vector3 FacingDirection { get; private set; } = Vector3.forward;
        public float RegimentExperience => units.Count > 0 ? GetAverageExperience() : 0f;
        public int RegimentRankIndex { get; private set; } = 0;
        public RegimentRank RegimentRankEnum => (RegimentRank)RegimentRankIndex;

        /// <summary>Cached alive count, updated every 0.3s. Use for non-critical reads.</summary>
        public int CachedAliveCount => cachedAliveCount;
        public float CachedAverageMorale => cachedAverageMorale;
        public float CachedAverageStamina => cachedAverageStamina;
        public int CachedTotalAmmo => cachedTotalAmmo;

        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < units.Count; i++)
                    if (units[i] != null && units[i].CurrentState != UnitState.Dead) count++;
                cachedAliveCount = count; // Update cache whenever computed
                return count;
            }
        }

        public float AverageMorale
        {
            get
            {
                float total = 0f;
                int alive = 0;
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i] != null && units[i].CurrentState != UnitState.Dead)
                    {
                        total += units[i].CurrentMorale;
                        alive++;
                    }
                }
                return alive > 0 ? total / alive : 0f;
            }
        }

        private void Update()
        {
            // Periodically refresh cached alive count
            aliveCountTimer -= Time.deltaTime;
            if (aliveCountTimer <= 0f)
            {
                aliveCountTimer = 0.3f;
                int count = 0;
                float totalMorale = 0f;
                float totalStamina = 0f;
                int totalAmmo = 0;
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u != null && u.CurrentState != UnitState.Dead)
                    {
                        count++;
                        totalMorale += u.CurrentMorale;
                        totalStamina += u.StaminaPercent;
                        totalAmmo += u.CurrentAmmo;
                    }
                }
                cachedAliveCount = count;
                cachedAverageMorale = count > 0 ? totalMorale / count : 0f;
                cachedAverageStamina = count > 0 ? totalStamina / count : 0f;
                cachedTotalAmmo = totalAmmo;
            }
            
            // Rigid Body Formation Movement
            if (isRegimentMoving)
            {
                // Advance center toward destination
                float distToTarget = Vector3.Distance(currentRegimentPosition, targetRegimentPosition);
                
                if (distToTarget < 0.1f && Quaternion.Angle(currentRegimentRotation, targetRegimentRotation) < 1f)
                {
                    // Arrived
                    currentRegimentPosition = targetRegimentPosition;
                    currentRegimentRotation = targetRegimentRotation;
                    isRegimentMoving = false;
                    hasMoveOrder = false;
                }
                else
                {
                    // Move position
                    if (distToTarget > 0.01f)
                    {
                        currentRegimentPosition = Vector3.MoveTowards(
                            currentRegimentPosition, 
                            targetRegimentPosition, 
                            regimentMoveSpeed * Time.deltaTime
                        );
                        currentRegimentPosition.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(currentRegimentPosition);
                    }
                    
                    // Rotate facing
                    currentRegimentRotation = Quaternion.RotateTowards(
                        currentRegimentRotation, 
                        targetRegimentRotation, 
                        30f * Time.deltaTime // Turn rate in degrees per second
                    );
                }
                
                // Recalculate slots based on the sliding center/rotation and push to units
                List<UnitBase> aliveUnits = GetAliveUnits();
                int n = aliveUnits.Count;
                if (n > 0)
                {
                    Vector3[] localFormation = GetFormationPositions(n);
                    for (int i = 0; i < n; i++)
                    {
                        if (!pooledSlotUsed[i]) continue;
                        
                        UnitBase assignedUnit = aliveUnits[pooledSlotAssignment[i]];
                        if (assignedUnit == null || assignedUnit.CurrentState == UnitState.Dead) continue;

                        Vector3 worldPos = currentRegimentPosition + (currentRegimentRotation * localFormation[i]);
                        worldPos.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(worldPos);
                        
                        // Push dynamic target strictly to the unit
                        assignedUnit.UpdateFormationTarget(worldPos, regimentMoveSpeed);
                        
                        // If we are stationary but just finishing rotation
                        if (!isRegimentMoving && Vector3.Distance(assignedUnit.transform.position, worldPos) < 0.2f)
                        {
                            assignedUnit.transform.rotation = Quaternion.RotateTowards(
                                assignedUnit.transform.rotation, 
                                currentRegimentRotation, 
                                90f * Time.deltaTime
                            );
                        }
                    }
                }
            }
            
            // Volley fire cooldown
            if (volleyCooldown > 0f)
            {
                volleyCooldown -= Time.deltaTime;
            }
            
            // Stand up units that have finished reloading (every 0.5s)
            if (isVolleyMode && Time.frameCount % 30 == 0)
            {
                StandUpReloadedUnits();
            }
        }

        public void Initialize(UnitData data, int count, int teamId)
        {
            unitData = data;
            TeamId = teamId;

            for (int i = 0; i < count; i++)
            {
                GameObject unitGO = CreateUnitVisual(data);
                unitGO.transform.SetParent(transform);
                unitGO.name = $"{data.unitName}_{i}";
                unitGO.layer = LayerMask.NameToLayer("Default");

                Rigidbody rb = unitGO.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                UnitBase unit = unitGO.AddComponent<UnitBase>();
                unit.SetUnitData(data);
                unit.Regiment = this;
                unit.TeamId = teamId;

                units.Add(unit);
            }

            ApplyFormation(true); // true = placement instantané à l'initialisation

            // Create firing range visualizer for ranged units (one per regiment)
            CreateFiringRangeVisualizer();
        }

        /// <summary>
        /// Set the regiment rank and apply stat bonuses to all units.
        /// Called after Initialize, typically from BattleSceneSetup using campaign data.
        /// </summary>
        public void SetRegimentRank(int rank)
        {
            RegimentRankIndex = Mathf.Clamp(rank, 0, RegimentRankSystem.TotalRanks - 1);
            foreach (var unit in units)
            {
                if (unit != null)
                    unit.ApplyRankBonuses(RegimentRankIndex);
            }
        }

        /// <summary>
        /// Create the firing range visualizer for this regiment if it's a ranged unit type.
        /// Only creates one visualizer per regiment, not per soldier.
        /// </summary>
        private void CreateFiringRangeVisualizer()
        {
            if (unitData == null) return;
            
            // Only create for ranged units (attack range > melee range + 1)
            bool isRanged = unitData.attackRange > unitData.meleeRange + 1f;
            if (!isRanged) return;

            // Create visualizer on the regiment game object
            rangeVisualizer = gameObject.AddComponent<FiringRangeVisualizer>();
            
            // Initially hidden - will show when selected
            if (rangeVisualizer != null)
                rangeVisualizer.Hide();
        }

        private GameObject CreateUnitVisual(UnitData data)
        {
            // Use UnitModelLoader if available (loads 3D models or falls back to primitives)
            if (UnitModelLoader.Instance != null)
                return UnitModelLoader.Instance.CreateUnitVisual(data);

            // Direct fallback if no UnitModelLoader in scene
            return CreatePrimitiveFallback(data);
        }

        private GameObject CreatePrimitiveFallback(UnitData data)
        {
            PrimitiveType shape;
            Vector3 scale;

            switch (data.unitType)
            {
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    shape = PrimitiveType.Cube;
                    scale = new Vector3(0.4f, 0.7f, 0.8f) * data.visualScaleMultiplier;
                    break;
                case UnitType.Artillery:
                    shape = PrimitiveType.Cylinder;
                    scale = new Vector3(0.6f, 0.3f, 0.6f) * data.visualScaleMultiplier;
                    break;
                case UnitType.Grenadier:
                    shape = PrimitiveType.Capsule;
                    scale = new Vector3(0.55f, 0.95f, 0.55f) * data.visualScaleMultiplier;
                    break;
                case UnitType.LightInfantry:
                    shape = PrimitiveType.Capsule;
                    scale = new Vector3(0.4f, 0.7f, 0.4f) * data.visualScaleMultiplier;
                    break;
                default:
                    shape = PrimitiveType.Capsule;
                    scale = new Vector3(0.5f, 0.8f, 0.5f) * data.visualScaleMultiplier;
                    break;
            }

            if (data.visualShape != UnitVisualShape.Capsule)
            {
                switch (data.visualShape)
                {
                    case UnitVisualShape.Cube: shape = PrimitiveType.Cube; break;
                    case UnitVisualShape.Cylinder: shape = PrimitiveType.Cylinder; break;
                    case UnitVisualShape.Sphere: shape = PrimitiveType.Sphere; break;
                }
            }

            GameObject go = GameObject.CreatePrimitive(shape);
            go.transform.localScale = scale;
            URPMaterialHelper.FixPrimitiveMaterial(go, data.factionColor);
            return go;
        }

        public void SetFormation(FormationType formation)
        {
            // Skip if already in this formation - prevents unnecessary repositioning
            if (currentFormation == formation) return;
            
            // Cancel any ongoing regiment march so units can freely reorganize
            isRegimentMoving = false;
            hasMoveOrder = false;
            
            currentFormation = formation;

            // Square formation gives anti-cavalry bonus via morale
            // Skirmish formation is only for light infantry
            if (formation == FormationType.Skirmish && unitData != null &&
                unitData.unitType != UnitType.LightInfantry)
            {
                currentFormation = FormationType.Line;
            }

            ApplyFormation();
        }

        /// <summary>
        /// Set formation type without moving units. Used by line extension feature.
        /// </summary>
        public void SetFormationTypeOnly(FormationType formation)
        {
            currentFormation = formation;

            // Square formation gives anti-cavalry bonus via morale
            // Skirmish formation is only for light infantry
            if (formation == FormationType.Skirmish && unitData != null &&
                unitData.unitType != UnitType.LightInfantry)
            {
                currentFormation = FormationType.Line;
            }
            // NOTE: Does NOT call ApplyFormation - units will NOT move
        }

        /// <summary>
        /// Adjust unit spacing (for line extension feature).
        /// This only updates the spacing value - actual movement is handled by MoveRegiment.
        /// </summary>
        public void SetUnitSpacing(float newSpacing)
        {
            unitSpacing = Mathf.Clamp(newSpacing, 0.5f, 5f);
            // NOTE: Do NOT call ApplyFormation here - MoveRegiment will handle the actual movement
        }

        /// <summary>
        /// Set the number of ranks (rows) in line formation.
        /// 1 = single rank, 2 = double rank, up to 5 ranks.
        /// This allows creating wide thin lines or deep formations.
        /// </summary>
        public void SetRankCount(int ranks)
        {
            desiredRanks = Mathf.Clamp(ranks, 0, 20); // 0 = auto
            // NOTE: Do NOT call ApplyFormation here - it is used during line extension preview
            // where MoveRegiment handles the actual movement on mouse release.
            // For standalone rank changes (PageUp/Down), the caller should call ApplyFormation.
        }

        /// <summary>
        /// Get current number of ranks setting.
        /// </summary>
        public int CurrentRankCount => ComputeEffectiveRanks(CachedAliveCount > 0 ? CachedAliveCount : units.Count);

        /// <summary>
        /// Compute the effective number of ranks for a given unit count.
        /// If desiredRanks > 0, uses that value. Otherwise auto-calculates
        /// to keep frontage between ~20-30 soldiers wide.
        /// </summary>
        private int ComputeEffectiveRanks(int unitCount)
        {
            if (desiredRanks > 0)
                return Mathf.Clamp(desiredRanks, 1, Mathf.Max(1, unitCount));

            // Auto-compute: target ~25 soldiers per front rank
            // This gives proportional depth for any regiment size
            if (unitCount <= 10)  return 1;  // Very small: single line
            if (unitCount <= 30)  return 2;  // Small: 2 ranks (15 wide)
            if (unitCount <= 60)  return 3;  // Medium: 3 ranks (20 wide)
            if (unitCount <= 120) return 4;  // Large: 4 ranks (30 wide)
            if (unitCount <= 200) return 6;  // Very large: 6 ranks (~33 wide)
            if (unitCount <= 300) return 8;  // Huge: 8 ranks (~37 wide)
            if (unitCount <= 500) return 10; // Massive: 10 ranks (40-50 wide)
            return Mathf.CeilToInt(unitCount / 40f); // Scale linearly for extreme sizes
        }

        /// <summary>
        /// Fire a volley from the current rank, then advance to next rank
        /// Front rank fires first, then kneels. Next rank fires, etc.
        /// </summary>
        public void FireVolleyRank(List<UnitBase> enemyAlive)
        {
            if (enemyAlive.Count == 0) return;
            if (!isVolleyMode) return;
            if (volleyCooldown > 0f) return;

            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;

            // Get units in current firing rank
            List<UnitBase> firingRankUnits = GetUnitsInRank(aliveUnits, currentFiringRank);
            
            if (firingRankUnits.Count == 0)
            {
                // No units in this rank, advance to next
                AdvanceFiringRank();
                return;
            }

            // Fire and kneel for each unit in current rank
            for (int i = 0; i < firingRankUnits.Count; i++)
            {
                UnitBase unit = firingRankUnits[i];
                if (unit == null || unit.CurrentState == UnitState.Dead) continue;
                if (unit.CurrentAmmo <= 0) continue;

                // Get target
                UnitBase target = enemyAlive[i % enemyAlive.Count];
                
                // Fire
                unit.AttackTarget(target);
                
                // Kneel after firing (will auto-stand after reload)
                unit.SetKneeling(true);
            }

            // Advance to next rank for next volley
            AdvanceFiringRank();
            
            // Set cooldown
            volleyCooldown = unitData != null ? unitData.volleyCooldown : 6f;
        }

        // Pooled lists for GetUnitsInRank to avoid GC allocations
        private List<UnitBase> pooledRankUnits = new List<UnitBase>(64);
        private List<(UnitBase unit, float localZ)> pooledSortedByZ = new List<(UnitBase, float)>(64);

        /// <summary>
        /// Get units belonging to a specific rank (0 = front, 1 = middle, 2 = back)
        /// WARNING: Returns pooled list - do NOT cache the result!
        /// </summary>
        private List<UnitBase> GetUnitsInRank(List<UnitBase> allUnits, int rank)
        {
            pooledRankUnits.Clear();
            pooledSortedByZ.Clear();
            
            // Formation layout: front rank has lowest Z (most forward), back rank highest Z
            // Sort by Z position relative to regiment
            for (int i = 0; i < allUnits.Count; i++)
            {
                UnitBase unit = allUnits[i];
                if (unit == null) continue;
                Vector3 localPos = transform.InverseTransformPoint(unit.transform.position);
                pooledSortedByZ.Add((unit, localPos.z));
            }
            
            // Sort by Z ascending (front to back)
            pooledSortedByZ.Sort((a, b) => a.localZ.CompareTo(b.localZ));
            
            // Determine rank size
            int rankSize = Mathf.Max(pooledSortedByZ.Count / 3, 1);
            
            // Get units for requested rank
            int startIdx = rank * rankSize;
            int endIdx = Mathf.Min(startIdx + rankSize, pooledSortedByZ.Count);
            
            for (int i = startIdx; i < endIdx && i < pooledSortedByZ.Count; i++)
            {
                pooledRankUnits.Add(pooledSortedByZ[i].unit);
            }
            
            return pooledRankUnits;
        }

        /// <summary>
        /// Check if a specific unit is in the front rank (rank 0) of the formation.
        /// Units are considered front rank if they are in the front third by Z position.
        /// </summary>
        public bool IsUnitInFrontRank(UnitBase unit)
        {
            if (unit == null) return false;
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count <= 1) return true; // Single unit is always "front"

            // Sort by local Z (front to back)
            float unitLocalZ = transform.InverseTransformPoint(unit.transform.position).z;
            int aheadCount = 0;
            for (int i = 0; i < aliveUnits.Count; i++)
            {
                if (aliveUnits[i] == unit) continue;
                float otherZ = transform.InverseTransformPoint(aliveUnits[i].transform.position).z;
                if (otherZ < unitLocalZ) aheadCount++;
            }
            
            // Front rank = in the first third of the formation
            int rankSize = Mathf.Max(aliveUnits.Count / 3, 1);
            return aheadCount < rankSize;
        }

        /// <summary>
        /// Advance to next firing rank (cycles through 0, 1, 2)
        /// </summary>
        private void AdvanceFiringRank()
        {
            currentFiringRank = (currentFiringRank + 1) % 3;
        }

        /// <summary>
        /// Reset volley fire rotation (call when volley mode is disabled)
        /// </summary>
        public void ResetVolleyRotation()
        {
            currentFiringRank = 0;
            
            // Make all units stand up
            foreach (var unit in units)
            {
                if (unit != null && unit.CurrentState != UnitState.Dead)
                {
                    unit.SetKneeling(false);
                }
            }
        }

        /// <summary>
        /// Stand up all units that have finished reloading
        /// Called periodically during volley fire
        /// </summary>
        public void StandUpReloadedUnits()
        {
            if (!isVolleyMode) return;
            
            foreach (var unit in units)
            {
                if (unit != null && unit.CurrentState != UnitState.Dead && unit.IsReloaded)
                {
                    unit.SetKneeling(false);
                }
            }
        }

        public void ToggleVolleyFire()
        {
            if (unitData != null && !unitData.canVolleyFire) return;

            isVolleyMode = !isVolleyMode;
            
            if (!isVolleyMode)
            {
                // Reset everything when disabling volley mode
                ResetVolleyRotation();
            }
            else
            {
                // Reset firing rank when enabling
                currentFiringRank = 0;
            }
            
            foreach (var unit in units)
            {
                if (unit != null && unit.CurrentState != UnitState.Dead)
                    unit.SetVolleyMode(isVolleyMode);
            }
        }

        public void SetVolleyMode(bool enabled)
        {
            if (unitData != null && !unitData.canVolleyFire) return;
            isVolleyMode = enabled;
            foreach (var unit in units)
            {
                if (unit != null && unit.CurrentState != UnitState.Dead)
                    unit.SetVolleyMode(enabled);
            }
        }

        public void ApplyFormation(bool instant = false)
        {
            // Cancel any ongoing regiment march
            isRegimentMoving = false;
            hasMoveOrder = false;
            
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;

            int n = aliveUnits.Count;
            Vector3[] positions = GetFormationPositions(n);
            
            // Re-center physical tracking around the regiment
            if (!isRegimentMoving && currentRegimentPosition == Vector3.zero)
            {
                currentRegimentPosition = transform.position;
                currentRegimentRotation = transform.rotation;
            }
            
            AssignUnitsToNearestSlots(aliveUnits, positions, n);
            for (int i = 0; i < n; i++)
            {
                int unitIdx = pooledSlotAssignment[i];
                UnitBase assignedUnit = aliveUnits[unitIdx];
                
                Vector3 worldPos = currentRegimentPosition + (currentRegimentRotation * positions[i]);
                worldPos.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(worldPos);
                
                if (instant)
                {
                    assignedUnit.transform.position = worldPos;
                    assignedUnit.transform.rotation = currentRegimentRotation;
                }
                
                assignedUnit.UpdateFormationTarget(worldPos, 0f);
            }
        }

        /// <summary>
        /// Get formation positions. WARNING: Returns pooled array - do NOT cache!
        /// </summary>
        public Vector3[] GetFormationPositions(int count)
        {
            // Ensure pooled array is large enough
            if (pooledFormationPositions.Length < count)
                pooledFormationPositions = new Vector3[count * 2];

            switch (currentFormation)
            {
                case FormationType.Line:
                    FillLineFormation(count);
                    break;
                case FormationType.Column:
                    FillColumnFormation(count);
                    break;
                case FormationType.Square:
                    FillSquareFormation(count);
                    break;
                case FormationType.Skirmish:
                    FillSkirmishFormation(count);
                    break;
                case FormationType.Wedge:
                    FillWedgeFormation(count);
                    break;
                case FormationType.Oblique:
                    FillObliqueFormation(count);
                    break;
                case FormationType.MixedOrder:
                    FillMixedOrderFormation(count);
                    break;
                default:
                    FillLineFormation(count);
                    break;
            }
            return pooledFormationPositions;
        }

        /// <summary>
        /// Calculate formation positions with custom spacing (for preview during line extension).
        /// Returns world positions given a center point, facing angle, and custom unit spacing.
        /// </summary>
        public Vector3[] GetFormationPositionsWithSpacing(int count, Vector3 center, float facingAngle, float customSpacing)
        {
            // Calculate local positions with custom spacing
            int rows = ComputeEffectiveRanks(count);
            int cols = Mathf.CeilToInt((float)count / rows);

            Vector3[] positions = new Vector3[count];
            int index = 0;

            float startX = -(cols - 1) * customSpacing * 0.5f;
            float startZ = -(rows - 1) * rowSpacing * 0.5f;

            Quaternion rotation = Quaternion.Euler(0f, facingAngle, 0f);

            for (int row = 0; row < rows && index < count; row++)
            {
                for (int col = 0; col < cols && index < count; col++)
                {
                    Vector3 localPos = new Vector3(
                        startX + col * customSpacing,
                        0f,
                        startZ + row * rowSpacing
                    );
                    // Rotate and translate to world position
                    positions[index] = center + rotation * localPos;
                    index++;
                }
            }

            return positions;
        }

        private void FillLineFormation(int count)
        {
            // Use configurable rank count (1-5 ranks/rows)
            int rows = ComputeEffectiveRanks(count);
            int cols = Mathf.CeilToInt((float)count / rows);

            int index = 0;
            // CENTER the formation around origin for standard MoveRegiment(dest)
            // Line-extension MoveRegiment(dest, angle) handles left-anchoring separately
            float startX = -(cols - 1) * unitSpacing * 0.5f;
            // Z starts positive (front) and decreases for each back row
            float startZ = (rows - 1) * rowSpacing * 0.5f;

            for (int row = 0; row < rows && index < count; row++)
            {
                // Center incomplete rows (last row with fewer units)
                int unitsThisRow = Mathf.Min(cols, count - index);
                float rowCenterOffset = (cols - unitsThisRow) * unitSpacing * 0.5f;
                
                for (int col = 0; col < unitsThisRow; col++)
                {
                    pooledFormationPositions[index].x = startX + rowCenterOffset + col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = startZ - row * rowSpacing;
                    index++;
                }
            }
        }

        private void AssignUnitsToFormation(List<UnitBase> aliveUnits, Vector3[] worldSlots, int count, Vector3 formationForward)
        {
            AssignUnitsToNearestSlots(aliveUnits, worldSlots, count);
        }

        private void AssignUnitsToNearestSlots(List<UnitBase> aliveUnits, Vector3[] worldSlots, int count)
        {
            if (pooledSlotAssignment.Length < count)
            {
                pooledSlotAssignment = new int[count * 2];
                pooledSlotUsed = new bool[count * 2];
            }

            for (int i = 0; i < count; i++) pooledSlotUsed[i] = false;

            // Simple greedy distance assignment
            for (int slot = 0; slot < count; slot++)
            {
                int bestUnit = -1;
                float bestDistSq = float.MaxValue;

                for (int u = 0; u < count; u++)
                {
                    if (pooledSlotUsed[u]) continue;

                    float distSq = (worldSlots[slot] - aliveUnits[u].transform.position).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestUnit = u;
                    }
                }

                if (bestUnit >= 0)
                {
                    pooledSlotAssignment[slot] = bestUnit;
                    pooledSlotUsed[bestUnit] = true;
                }
            }
        }

        private void FillColumnFormation(int count)
        {
            int cols = 4;
            int rows = Mathf.CeilToInt((float)count / cols);

            int index = 0;
            float startX = -(cols - 1) * unitSpacing * 0.5f;

            for (int row = 0; row < rows && index < count; row++)
            {
                for (int col = 0; col < cols && index < count; col++)
                {
                    pooledFormationPositions[index].x = startX + col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = -row * rowSpacing;
                    index++;
                }
            }
        }

        private void FillSquareFormation(int count)
        {
            int sideLength = Mathf.CeilToInt(Mathf.Sqrt(count));
            int index = 0;
            float halfSize = (sideLength - 1) * unitSpacing * 0.5f;

            for (int i = 0; i < sideLength && index < count; i++)
            {
                pooledFormationPositions[index].x = -halfSize + i * unitSpacing;
                pooledFormationPositions[index].y = 0f;
                pooledFormationPositions[index].z = halfSize;
                index++;
                if (index >= count) break;
            }

            for (int i = 1; i < sideLength - 1 && index < count; i++)
            {
                pooledFormationPositions[index].x = halfSize;
                pooledFormationPositions[index].y = 0f;
                pooledFormationPositions[index].z = halfSize - i * unitSpacing;
                index++;
                if (index >= count) break;

                pooledFormationPositions[index].x = -halfSize;
                pooledFormationPositions[index].y = 0f;
                pooledFormationPositions[index].z = halfSize - i * unitSpacing;
                index++;
                if (index >= count) break;
            }

            for (int i = 0; i < sideLength && index < count; i++)
            {
                pooledFormationPositions[index].x = -halfSize + i * unitSpacing;
                pooledFormationPositions[index].y = 0f;
                pooledFormationPositions[index].z = -halfSize;
                index++;
                if (index >= count) break;
            }

            for (int row = 1; row < sideLength - 1 && index < count; row++)
            {
                for (int col = 1; col < sideLength - 1 && index < count; col++)
                {
                    pooledFormationPositions[index].x = -halfSize + col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = halfSize - row * unitSpacing;
                    index++;
                }
            }
        }

        private void FillSkirmishFormation(int count)
        {
            // Loose, spread-out formation for light infantry
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count * 2f));
            int rows = Mathf.CeilToInt((float)count / cols);
            float looseSpacing = unitSpacing * 2.5f;

            int index = 0;
            float startX = -(cols - 1) * looseSpacing * 0.5f;

            for (int row = 0; row < rows && index < count; row++)
            {
                float rowOffset = (row % 2 == 0) ? 0f : looseSpacing * 0.5f;
                for (int col = 0; col < cols && index < count; col++)
                {
                    pooledFormationPositions[index].x = startX + col * looseSpacing + rowOffset;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = -row * looseSpacing;
                    index++;
                }
            }
        }

        /// <summary>
        /// Wedge (V) formation — cavalry charge formation.
        /// Point man at front, expanding V-shape behind.
        /// Historically used by cavalry to pierce enemy lines.
        /// </summary>
        private void FillWedgeFormation(int count)
        {
            int index = 0;
            int rowsNeeded = 0;
            int unitsPlaced = 0;
            // Calculate how many rows we need
            for (int r = 0; unitsPlaced < count; r++)
            {
                unitsPlaced += (r * 2 + 1);
                rowsNeeded = r + 1;
            }

            int unitIdx = 0;
            for (int row = 0; row < rowsNeeded && unitIdx < count; row++)
            {
                int unitsInRow = row * 2 + 1; // 1, 3, 5, 7, ...
                float startX = -(unitsInRow - 1) * unitSpacing * 0.5f;
                for (int col = 0; col < unitsInRow && unitIdx < count; col++)
                {
                    pooledFormationPositions[unitIdx].x = startX + col * unitSpacing;
                    pooledFormationPositions[unitIdx].y = 0f;
                    pooledFormationPositions[unitIdx].z = -row * rowSpacing;
                    unitIdx++;
                }
            }
        }

        /// <summary>
        /// Oblique Line formation — Frederick the Great's innovation.
        /// A diagonal line where one flank is advanced ahead of the other,
        /// concentrating force on one point while refusing the other flank.
        /// Used at Leuthen (1757) and adopted by Napoleon.
        /// </summary>
        private void FillObliqueFormation(int count)
        {
            int rows = ComputeEffectiveRanks(count);
            int cols = Mathf.CeilToInt((float)count / rows);
            
            float startX = -(cols - 1) * unitSpacing * 0.5f;
            float startZ = -(rows - 1) * rowSpacing * 0.5f;
            
            // Oblique angle: each column is staggered forward
            // Creates a diagonal line from left-rear to right-front
            float obliqueStagger = rowSpacing * 0.6f; // How much diagonal offset per column

            int index = 0;
            for (int row = 0; row < rows && index < count; row++)
            {
                for (int col = 0; col < cols && index < count; col++)
                {
                    pooledFormationPositions[index].x = startX + col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    // Each column advances slightly forward (creating the oblique diagonal)
                    pooledFormationPositions[index].z = startZ + row * rowSpacing + col * obliqueStagger;
                    index++;
                }
            }
        }

        /// <summary>
        /// Mixed Order (Ordre Mixte) — Napoleon's signature formation.
        /// Center battalion in column, flanking battalions in line.
        /// Combines the firepower of line with the shock of column.
        /// The formation that won Austerlitz.
        /// </summary>
        private void FillMixedOrderFormation(int count)
        {
            if (count <= 6)
            {
                FillLineFormation(count); // Too few for mixed order
                return;
            }

            int index = 0;
            
            // Split into 3 sections: left wing (line), center (column), right wing (line)
            int centerCount = Mathf.Max(count / 3, 4);
            int wingCount = (count - centerCount) / 2;
            int rightWingCount = count - centerCount - wingCount; // Handles odd numbers
            
            // === CENTER COLUMN (dense, deep) ===
            int colCols = 4; // Narrow column
            int colRows = Mathf.CeilToInt((float)centerCount / colCols);
            float centerStartX = -(colCols - 1) * unitSpacing * 0.5f;
            
            for (int row = 0; row < colRows && index < centerCount; row++)
            {
                for (int col = 0; col < colCols && index < centerCount; col++)
                {
                    pooledFormationPositions[index].x = centerStartX + col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = -row * rowSpacing;
                    index++;
                }
            }
            
            // === LEFT WING (thin line, slightly behind) ===
            int lineRows = 2; // Thin line
            int lineCols = Mathf.CeilToInt((float)wingCount / lineRows);
            float wingOffset = (colCols * unitSpacing * 0.5f) + unitSpacing * 2f; // Gap from center
            float lineStartZ = -rowSpacing; // Slightly behind center column tip
            
            for (int row = 0; row < lineRows && index < centerCount + wingCount; row++)
            {
                for (int col = 0; col < lineCols && index < centerCount + wingCount; col++)
                {
                    pooledFormationPositions[index].x = -wingOffset - col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = lineStartZ + row * rowSpacing;
                    index++;
                }
            }
            
            // === RIGHT WING (thin line, slightly behind) ===
            int rightLineCols = Mathf.CeilToInt((float)rightWingCount / lineRows);
            
            for (int row = 0; row < lineRows && index < count; row++)
            {
                for (int col = 0; col < rightLineCols && index < count; col++)
                {
                    pooledFormationPositions[index].x = wingOffset + col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = lineStartZ + row * rowSpacing;
                    index++;
                }
            }
        }


        // Smooth regiment movement — Rigid Body Formation
        private Vector3 currentRegimentPosition;
        private Quaternion currentRegimentRotation = Quaternion.identity;
        private Vector3 targetRegimentPosition;
        private Quaternion targetRegimentRotation = Quaternion.identity;
        private bool isRegimentMoving = false;
        
        // Cooldowns
        private Vector3 rawMoveDestination;
        private bool hasMoveOrder = false;
        private float moveCooldown = 0f;
        private const float MOVE_COOLDOWN_TIME = 1.0f;

        // Synchronized move speed
        private float regimentMoveSpeed = 0f;
        public float RegimentMoveSpeed => regimentMoveSpeed;
        public bool IsRegimentMoving => isRegimentMoving;

        /// <summary>
        /// Absolute Rank Preservation:
        /// Identity mapping ensures that Soldier i always goes to Slot i.
        /// Because slots are mathematically generated Front-to-Back, Left-to-Right,
        /// this guarantees the hierarchy is never broken.
        /// </summary>
        private void AssignUnitsToFormation(int count)
        {
            if (pooledSlotAssignment.Length < count)
            {
                pooledSlotAssignment = new int[count * 2];
                pooledSlotUsed = new bool[count * 2];
            }
            
            for (int i = 0; i < count; i++)
            {
                pooledSlotAssignment[i] = i;
                pooledSlotUsed[i] = true;
            }
        }

        public void MoveRegiment(Vector3 destination)
        {
            destination.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(destination);
            
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;
            
            // === MOVE COOLDOWN: Prevent AI from spamming move orders ===
            if (moveCooldown > 0f)
            {
                moveCooldown -= Time.deltaTime;
                Vector3 cooldownDiff = destination - rawMoveDestination;
                cooldownDiff.y = 0f;
                if (cooldownDiff.sqrMagnitude < 400f)
                    return;
            }
            
            // === JITTER CHECK ===
            float jitterThreshold = unitData != null ? Mathf.Max(5.0f, unitData.moveSpeed * 1.2f) : 5.0f;
            Vector3 jitterDiff = destination - rawMoveDestination;
            jitterDiff.y = 0f;
            if (hasMoveOrder && jitterDiff.sqrMagnitude < jitterThreshold * jitterThreshold)
                return;
            
            int n = aliveUnits.Count;
            
            // Calculate current physical center if not moving
            if (!isRegimentMoving)
            {
                currentRegimentPosition = Vector3.zero;
                for (int i = 0; i < n; i++)
                    currentRegimentPosition += aliveUnits[i].transform.position;
                currentRegimentPosition /= n;
                currentRegimentPosition.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(currentRegimentPosition);
                
                currentRegimentRotation = Quaternion.LookRotation(FacingDirection.sqrMagnitude > 0.01f ? FacingDirection : Vector3.forward);
            }
            
            Vector3 moveDirection = destination - currentRegimentPosition;
            moveDirection.y = 0f;
            
            if (moveDirection.sqrMagnitude > 1f)
            {
                targetRegimentRotation = Quaternion.LookRotation(moveDirection.normalized);
                FacingDirection = moveDirection.normalized;
            }
            else
            {
                targetRegimentRotation = currentRegimentRotation;
            }
            
            // Re-assign units strictly to grid slots
            AssignUnitsToFormation(n);
            
            // Define destination
            targetRegimentPosition = destination;
            
            // Speed = slowest unit
            regimentMoveSpeed = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float spd = aliveUnits[i].GetEffectiveMoveSpeed();
                if (spd < regimentMoveSpeed) regimentMoveSpeed = spd;
            }
            if (regimentMoveSpeed >= float.MaxValue) regimentMoveSpeed = 3.5f;
            if (regimentMoveSpeed < 1f) regimentMoveSpeed = 1f;
            
            rawMoveDestination = destination;
            hasMoveOrder = true;
            moveCooldown = MOVE_COOLDOWN_TIME;
            isRegimentMoving = true;
        }

        public void MoveRegiment(Vector3 destination, float facingAngle)
        {
            destination.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(destination);
            
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;
            
            targetRegimentRotation = Quaternion.Euler(0f, facingAngle, 0f);
            FacingDirection = targetRegimentRotation * Vector3.forward;
            
            int n = aliveUnits.Count;
            
            if (!isRegimentMoving)
            {
                currentRegimentPosition = Vector3.zero;
                for (int i = 0; i < n; i++)
                    currentRegimentPosition += aliveUnits[i].transform.position;
                currentRegimentPosition /= n;
                currentRegimentPosition.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(currentRegimentPosition);
                
                currentRegimentRotation = targetRegimentRotation; // Snap rotation if stationary
            }
            
            AssignUnitsToFormation(n);
            
            // Compute destination centroid from the pre-calculated left-anchor geometry
            // The destination passed in is the left edge. We need the logic to find the true centroid.
            int rows = ComputeEffectiveRanks(n);
            int unitsPerRank = Mathf.CeilToInt((float)n / rows);
            int lastRowCount = n - (rows - 1) * unitsPerRank;
            if (rows > 1 && lastRowCount > 0 && lastRowCount < unitsPerRank * 0.4f)
            {
                rows = Mathf.Max(1, rows - 1);
                unitsPerRank = Mathf.CeilToInt((float)n / rows);
            }
            
            Vector3 centroid = Vector3.zero;
            int index = 0;
            for (int row = 0; row < rows && index < n; row++)
            {
                int unitsThisRow = Mathf.Min(unitsPerRank, n - index);
                float rowOffsetX = (unitsPerRank - unitsThisRow) * unitSpacing * 0.5f;
                
                for (int col = 0; col < unitsThisRow; col++)
                {
                    Vector3 localPos = new Vector3(rowOffsetX + col * unitSpacing, 0f, -row * rowSpacing);
                    Vector3 rotatedOffset = targetRegimentRotation * localPos;
                    centroid += destination + rotatedOffset;
                    index++;
                }
            }
            centroid /= n;
            centroid.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(centroid);
            
            targetRegimentPosition = centroid;
            
            regimentMoveSpeed = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float spd = aliveUnits[i].GetEffectiveMoveSpeed();
                if (spd < regimentMoveSpeed) regimentMoveSpeed = spd;
            }
            if (regimentMoveSpeed >= float.MaxValue) regimentMoveSpeed = 3.5f;
            if (regimentMoveSpeed < 1f) regimentMoveSpeed = 1f;
            
            rawMoveDestination = destination;
            hasMoveOrder = true;
            isRegimentMoving = true;
        }

        public void MoveRegiment(Vector3 destination, float facingAngle, Vector3[] precomputedWorldPositions)
        {
            destination.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(destination);
            
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;
            
            targetRegimentRotation = Quaternion.Euler(0f, facingAngle, 0f);
            FacingDirection = targetRegimentRotation * Vector3.forward;
            
            int n = Mathf.Min(aliveUnits.Count, precomputedWorldPositions.Length);
            
            if (!isRegimentMoving)
            {
                currentRegimentPosition = Vector3.zero;
                for (int i = 0; i < n; i++)
                    currentRegimentPosition += aliveUnits[i].transform.position;
                currentRegimentPosition /= n;
                currentRegimentPosition.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(currentRegimentPosition);
                
                currentRegimentRotation = targetRegimentRotation;
            }
            
            AssignUnitsToFormation(n);
            
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                centroid += precomputedWorldPositions[i];
            }
            centroid /= n;
            centroid.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(centroid);
            
            targetRegimentPosition = centroid;
            
            regimentMoveSpeed = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float spd = aliveUnits[i].GetEffectiveMoveSpeed();
                if (spd < regimentMoveSpeed) regimentMoveSpeed = spd;
            }
            if (regimentMoveSpeed >= float.MaxValue) regimentMoveSpeed = 3.5f;
            if (regimentMoveSpeed < 1f) regimentMoveSpeed = 1f;
            
            rawMoveDestination = destination;
            hasMoveOrder = true;
            isRegimentMoving = true;
        }

        public void RotateRegiment(Vector3 lookDirection)
        {
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.01f)
            {
                transform.forward = lookDirection.normalized;
            }
        }

        public void AttackTargetRegiment(Regiment enemyRegiment)
        {
            List<UnitBase> aliveUnits = GetAliveUnits();
            List<UnitBase> enemyAlive = enemyRegiment.GetAliveUnits();

            if (enemyAlive.Count == 0) return;

            for (int i = 0; i < aliveUnits.Count; i++)
            {
                UnitBase target = enemyAlive[i % enemyAlive.Count];
                aliveUnits[i].AttackTarget(target);
            }
        }

        public void ChargeTargetRegiment(Regiment enemyRegiment)
        {
            if (unitData != null && !unitData.canCharge)
            {
                AttackTargetRegiment(enemyRegiment);
                return;
            }

            List<UnitBase> aliveUnits = GetAliveUnits();
            List<UnitBase> enemyAlive = enemyRegiment.GetAliveUnits();

            if (enemyAlive.Count == 0) return;

            for (int i = 0; i < aliveUnits.Count; i++)
            {
                UnitBase target = enemyAlive[i % enemyAlive.Count];
                aliveUnits[i].ChargeTarget(target);
            }
        }

        public void ApplyMoraleShock(float amount)
        {
            foreach (var unit in units)
            {
                if (unit != null && unit.CurrentState != UnitState.Dead)
                {
                    unit.ApplyMoraleDamage(amount);
                }
            }
        }

        /// <summary>
        /// Rally fleeing units — restore some morale and return them to Idle.
        /// Only works if the officer is alive. Cooldown managed externally.
        /// </summary>
        public int Rally()
        {
            float moraleBoost = OfficerAlive ? 35f : 15f;
            int rallied = 0;

            foreach (var unit in units)
            {
                if (unit != null && unit.CurrentState == UnitState.Fleeing)
                {
                    unit.RestoreMorale(moraleBoost);
                    unit.ForceState(UnitState.Idle);
                    rallied++;
                }
            }

            if (rallied > 0 && NapoleonicWars.UI.BattleLogUI.Instance != null)
            {
                string msg = $"{regimentName}: {rallied} units rallied!";
                if (TeamId == 0)
                    NapoleonicWars.UI.BattleLogUI.Instance.LogPlayerEvent(msg);
                else
                    NapoleonicWars.UI.BattleLogUI.Instance.LogEnemyEvent(msg);
            }

            return rallied;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            
            // Show/hide firing range visualizer for the entire regiment
            if (rangeVisualizer != null)
            {
                if (selected)
                    rangeVisualizer.Show();
                else
                    rangeVisualizer.Hide();
            }
            
            // Propagate selection to individual units (for highlighting, etc.)
            foreach (var unit in units)
            {
                if (unit != null && unit.CurrentState != UnitState.Dead)
                    unit.SetSelected(selected);
            }
        }

        public void OnUnitDamaged(UnitBase unit)
        {
            // Propagate morale loss to nearby units (use sqrMagnitude)
            float moraleLoss = unitData != null ? unitData.moraleLossOnDeath : 2f;
            float px = unit.transform.position.x;
            float pz = unit.transform.position.z;

            for (int i = 0; i < units.Count; i++)
            {
                UnitBase u = units[i];
                if (u == null || u == unit || u.CurrentState == UnitState.Dead) continue;
                float dx = u.transform.position.x - px;
                float dz = u.transform.position.z - pz;
                if (dx * dx + dz * dz < 25f) // 5^2
                    u.ApplyMoraleDamage(moraleLoss * 0.3f);
            }
        }

        public void OnUnitDied(UnitBase unit)
        {
            // Morale shock from seeing a comrade die (use sqrMagnitude)
            float moraleLoss = unitData != null ? unitData.moraleLossOnDeath : 2f;
            float px = unit.transform.position.x;
            float pz = unit.transform.position.z;

            for (int i = 0; i < units.Count; i++)
            {
                UnitBase u = units[i];
                if (u == null || u == unit || u.CurrentState == UnitState.Dead) continue;
                float dx = u.transform.position.x - px;
                float dz = u.transform.position.z - pz;
                float distSqr = dx * dx + dz * dz;
                if (distSqr < 64f) // 8^2
                    u.ApplyMoraleDamage(moraleLoss);
                else
                    u.ApplyMoraleDamage(moraleLoss * 0.3f);
            }

            cachedAliveCount = Mathf.Max(0, cachedAliveCount - 1);

            if (cachedAliveCount <= 0)
            {
                Debug.Log($"Regiment {regimentName} has been destroyed!");
                if (NapoleonicWars.UI.BattleLogUI.Instance != null)
                {
                    if (TeamId == 0)
                        NapoleonicWars.UI.BattleLogUI.Instance.LogPlayerEvent($"{regimentName} destroyed!");
                    else
                        NapoleonicWars.UI.BattleLogUI.Instance.LogEnemyEvent($"{regimentName} destroyed!");
                }
            }
        }

        /// <summary>
        /// Returns alive units using a pooled list. WARNING: do NOT cache the result.
        /// The list is reused on next call.
        /// </summary>
        public List<UnitBase> GetAliveUnits()
        {
            pooledAliveList.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].CurrentState != UnitState.Dead)
                    pooledAliveList.Add(units[i]);
            }
            return pooledAliveList;
        }

        public bool IsInSquareFormation()
        {
            return currentFormation == FormationType.Square;
        }

        private float GetAverageExperience()
        {
            float total = 0f;
            int count = 0;
            foreach (var u in units)
            {
                if (u != null && u.CurrentState != UnitState.Dead)
                {
                    total += u.Experience;
                    count++;
                }
            }
            return count > 0 ? total / count : 0f;
        }

        /// <summary>
        /// Called when a unit dies. Small chance the officer is killed too.
        /// </summary>
        public void CheckOfficerCasualty()
        {
            if (!OfficerAlive) return;
            // 3% chance per death that the officer is hit
            if (Random.value < 0.03f)
            {
                OfficerAlive = false;
                Debug.Log($"[{regimentName}] Officer killed! Morale will suffer.");
                ApplyMoraleShock(20f);

                if (NapoleonicWars.UI.BattleLogUI.Instance != null)
                {
                    if (TeamId == 0)
                        NapoleonicWars.UI.BattleLogUI.Instance.LogPlayerEvent($"{regimentName}: Officer killed!");
                    else
                        NapoleonicWars.UI.BattleLogUI.Instance.LogEnemyEvent($"{regimentName}: Officer killed!");
                }

                if (BattleStatistics.Instance != null)
                    BattleStatistics.Instance.RecordOfficerLost(TeamId);
            }
        }

        /// <summary>
        /// Get total remaining ammo across all alive units.
        /// </summary>
        public int TotalAmmo
        {
            get
            {
                int total = 0;
                foreach (var u in units)
                    if (u != null && u.CurrentState != UnitState.Dead)
                        total += u.CurrentAmmo;
                return total;
            }
        }

        /// <summary>
        /// Get average stamina percentage across alive units.
        /// </summary>
        public float AverageStaminaPercent
        {
            get
            {
                float total = 0f;
                int count = 0;
                foreach (var u in units)
                {
                    if (u != null && u.CurrentState != UnitState.Dead)
                    {
                        total += u.StaminaPercent;
                        count++;
                    }
                }
                return count > 0 ? total / count : 0f;
            }
        }
    }
}
