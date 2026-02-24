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
            
            // DO NOT slerp the parent rotation while units are moving!
            // Units are children of this transform. Rotating the parent DRAGS all units,
            // conflicting with their individual world-space MoveTo targets.
            // This was the cause of teleportation and front/back rank swapping.
            if (isRegimentMoving)
            {
                // Check if most units have arrived at their targets
                List<UnitBase> alive = GetAliveUnits();
                int arrived = 0;
                for (int i = 0; i < alive.Count; i++)
                {
                    if (alive[i].CurrentState == UnitState.Idle || alive[i].CurrentState == UnitState.Attacking)
                        arrived++;
                }
                
                // Once most units arrive, stop the movement flag
                if (arrived >= alive.Count * 0.8f || alive.Count == 0)
                {
                    isRegimentMoving = false;
                    hasMoveOrder = false;
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
            // NOTE: Do NOT call ApplyFormation here - MoveRegiment will handle the actual movement
            // Calling ApplyFormation during line extension preview sends units backward
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
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;

            // Only teleport instantly when explicitly requested (e.g., initial unit creation)
            // Never teleport during gameplay - always use smooth movement
            bool useInstant = instant;

            Vector3[] positions = GetFormationPositions(aliveUnits.Count);
            
            // Compute world positions
            int n = aliveUnits.Count;
            if (pooledFormationWorldPos == null || pooledFormationWorldPos.Length < n)
                pooledFormationWorldPos = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                pooledFormationWorldPos[i] = transform.TransformPoint(positions[i]);
                pooledFormationWorldPos[i].y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(pooledFormationWorldPos[i]);
            }
            
            // Assign units to nearest slots to prevent them from crossing paths
            AssignUnitsToNearestSlots(aliveUnits, pooledFormationWorldPos, n);

            for (int i = 0; i < n; i++)
            {
                Vector3 worldPos = pooledFormationWorldPos[i];
                int unitIdx = pooledSlotAssignment[i];
                
                if (useInstant)
                {
                    // Instant teleport - only for initial creation, never during gameplay
                    aliveUnits[unitIdx].transform.position = worldPos;
                    aliveUnits[unitIdx].MoveTo(worldPos);
                }
                else
                {
                    // Smooth movement - units will walk to formation position
                    aliveUnits[unitIdx].MoveTo(worldPos);
                }
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
            float startZ = -(rows - 1) * rowSpacing * 0.5f;

            for (int row = 0; row < rows && index < count; row++)
            {
                for (int col = 0; col < cols && index < count; col++)
                {
                    pooledFormationPositions[index].x = startX + col * unitSpacing;
                    pooledFormationPositions[index].y = 0f;
                    pooledFormationPositions[index].z = startZ + row * rowSpacing;
                    index++;
                }
            }
        }

        /// <summary>
        /// Greedy nearest-slot assignment: for each formation slot, find the closest 
        /// unassigned soldier. Result is stored in pooledSlotAssignment[slotIndex] = unitIndex.
        /// This prevents soldiers from crossing over each other when reforming.
        /// </summary>
        private void AssignUnitsToNearestSlots(List<UnitBase> aliveUnits, Vector3[] worldSlots, int count)
        {
            // Ensure pooled arrays are big enough
            if (pooledSlotAssignment.Length < count)
            {
                pooledSlotAssignment = new int[count * 2];
                pooledSlotUsed = new bool[count * 2];
            }
            
            // Reset used flags
            for (int i = 0; i < count; i++)
                pooledSlotUsed[i] = false;

            // For each slot, find the closest unassigned unit
            for (int slot = 0; slot < count; slot++)
            {
                float bestDistSqr = float.MaxValue;
                int bestUnit = -1;
                Vector3 slotPos = worldSlots[slot];

                for (int u = 0; u < count; u++)
                {
                    if (pooledSlotUsed[u]) continue;
                    
                    Vector3 unitPos = aliveUnits[u].transform.position;
                    float dx = unitPos.x - slotPos.x;
                    float dz = unitPos.z - slotPos.z;
                    float distSqr = dx * dx + dz * dz;
                    
                    if (distSqr < bestDistSqr)
                    {
                        bestDistSqr = distSqr;
                        bestUnit = u;
                    }
                }

                pooledSlotAssignment[slot] = bestUnit >= 0 ? bestUnit : slot;
                if (bestUnit >= 0)
                    pooledSlotUsed[bestUnit] = true;
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


        private Vector3 currentDestination;
        private bool hasMoveOrder = false;
        private float moveCooldown = 0f; // Prevents AI from re-issuing moves too rapidly
        private const float MOVE_COOLDOWN_TIME = 1.0f;

        // Smooth regiment movement — prevents teleportation
        private Vector3 targetRegimentPosition;
        private Quaternion targetRegimentFacing = Quaternion.identity;
        private bool isRegimentMoving = false;

        public void MoveRegiment(Vector3 destination)
        {
            // Get terrain height at destination
            destination.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(destination);
            
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;
            
            // === MOVE COOLDOWN: Prevent AI from spamming move orders ===
            if (moveCooldown > 0f)
            {
                moveCooldown -= Time.deltaTime;
                // During cooldown, only allow if destination is VERY different (>20 units)
                if ((destination - currentDestination).sqrMagnitude < 400f)
                    return;
            }
            
            // === JITTER CHECK: Scale threshold by unit speed ===
            float jitterThreshold = unitData != null ? Mathf.Max(5.0f, unitData.moveSpeed * 1.2f) : 5.0f;
            if (hasMoveOrder && (destination - currentDestination).sqrMagnitude < jitterThreshold * jitterThreshold)
            {
                return;
            }
            
            // Calculate direction from current position to destination
            Vector3 moveDirection = destination - transform.position;
            moveDirection.y = 0f;
            
            // Smooth regiment facing direction
            if (moveDirection.sqrMagnitude > 1f)
            {
                targetRegimentFacing = Quaternion.LookRotation(moveDirection.normalized);
                FacingDirection = moveDirection.normalized;
            }
            
            // Compute the facing rotation for formation layout
            Quaternion formationRotation = targetRegimentFacing != Quaternion.identity 
                ? targetRegimentFacing 
                : transform.rotation;
            
            // Get formation positions (local positions centered around 0,0,0)
            Vector3[] formationPositions = GetFormationPositions(aliveUnits.Count);
            
            // Compute WORLD positions for each slot relative to DESTINATION
            // DO NOT move the parent — compute everything in world space
            int n = aliveUnits.Count;
            if (pooledFormationWorldPos == null || pooledFormationWorldPos.Length < n)
                pooledFormationWorldPos = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                Vector3 rotatedOffset = formationRotation * formationPositions[i];
                pooledFormationWorldPos[i] = destination + rotatedOffset;
                pooledFormationWorldPos[i].y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(pooledFormationWorldPos[i]);
            }
            
            // Assign each unit to nearest available slot (prevents crossing paths)
            AssignUnitsToNearestSlots(aliveUnits, pooledFormationWorldPos, n);
            
            // DO NOT snap the parent transform — this was causing teleportation!
            // Units will walk to their world-space targets naturally via MoveTo.
            // Parent position stays unchanged; facing rotation slerps in Update.
            
            for (int i = 0; i < n; i++)
            {
                aliveUnits[pooledSlotAssignment[i]].MoveTo(pooledFormationWorldPos[i]);
            }
            
            currentDestination = destination;
            hasMoveOrder = true;
            moveCooldown = MOVE_COOLDOWN_TIME;
            isRegimentMoving = true;
        }

        /// <summary>
        /// Move regiment to destination with a specific facing angle (in degrees).
        /// Used for line extension feature where user drags to define line width and facing.
        /// ANCHORED AT LEFT: destination is position of leftmost unit, formation extends to the right.
        /// </summary>
        public void MoveRegiment(Vector3 destination, float facingAngle)
        {
            // Get terrain height at destination
            destination.y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(destination);
            
            List<UnitBase> aliveUnits = GetAliveUnits();
            if (aliveUnits.Count == 0) return;
            
            // Set target regiment facing direction from angle
            targetRegimentFacing = Quaternion.Euler(0f, facingAngle, 0f);
            FacingDirection = targetRegimentFacing * Vector3.forward;
            
            // Use the target facing for computing formation layout
            Quaternion rotation = targetRegimentFacing;
            int unitCount = aliveUnits.Count;
            int rows = ComputeEffectiveRanks(unitCount);
            int unitsPerRank = Mathf.CeilToInt((float)unitCount / rows);
            
            // Avoid tiny last rank: if last row has less than 40% of full row, reduce rows
            int lastRowCount = unitCount - (rows - 1) * unitsPerRank;
            if (rows > 1 && lastRowCount > 0 && lastRowCount < unitsPerRank * 0.4f)
            {
                rows = Mathf.Max(1, rows - 1);
                unitsPerRank = Mathf.CeilToInt((float)unitCount / rows);
            }
            
            // Build all WORLD positions (relative to destination, not parent)
            if (pooledFormationWorldPos == null || pooledFormationWorldPos.Length < unitCount)
                pooledFormationWorldPos = new Vector3[unitCount * 2];
            
            int index = 0;
            for (int row = 0; row < rows && index < unitCount; row++)
            {
                int unitsThisRow = Mathf.Min(unitsPerRank, unitCount - index);
                float rowOffsetX = (unitsPerRank - unitsThisRow) * unitSpacing * 0.5f;
                
                for (int col = 0; col < unitsThisRow; col++)
                {
                    // Row 0 = front rank (at destination), deeper rows go BEHIND
                    Vector3 localPos = new Vector3(
                        rowOffsetX + col * unitSpacing,
                        0f,
                        -row * rowSpacing
                    );
                    Vector3 rotatedOffset = rotation * localPos;
                    pooledFormationWorldPos[index] = destination + rotatedOffset;
                    pooledFormationWorldPos[index].y = NapoleonicWars.Core.BattleManager.GetTerrainHeight(pooledFormationWorldPos[index]);
                    index++;
                }
            }
            
            // Assign units to nearest slots
            AssignUnitsToNearestSlots(aliveUnits, pooledFormationWorldPos, unitCount);
            
            // DO NOT snap parent transform — let units walk to their world-space targets
            for (int i = 0; i < unitCount; i++)
            {
                aliveUnits[pooledSlotAssignment[i]].MoveTo(pooledFormationWorldPos[i]);
            }
            
            currentDestination = destination;
            hasMoveOrder = true;
            isRegimentMoving = true; // Facing will slerp in Update
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
