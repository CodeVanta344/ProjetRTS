using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Core;
using NapoleonicWars.Units;
using NapoleonicWars.Data;

namespace NapoleonicWars.AI
{
    /// <summary>
    /// Army-level strategic brain for the enemy AI.
    /// Detects battle phases, allocates forces into tactical groups,
    /// and issues coordination orders before individual regiment BTs run.
    /// </summary>
    public class BattleAIBrain
    {
        // ── Current strategic state ──
        public BattlePhase CurrentPhase { get; private set; } = BattlePhase.Opening;
        public float BattleTime { get; private set; }

        // ── Force groups ──
        public List<Regiment> MainLine { get; private set; } = new List<Regiment>();
        public List<Regiment> Reserve { get; private set; } = new List<Regiment>();
        public List<Regiment> FlankingForce { get; private set; } = new List<Regiment>();
        public List<Regiment> SkirmishScreen { get; private set; } = new List<Regiment>();
        public List<Regiment> ArtillerySupport { get; private set; } = new List<Regiment>();

        // ── Focus fire target ──
        public Regiment FocusFireTarget { get; private set; }

        // ── Internal state ──
        private bool forcesAllocated = false;
        private bool reserveCommitted = false;
        private float phaseCheckTimer = 0f;
        private float focusFireTimer = 0f;
        private const float PhaseCheckInterval = 2f;
        private const float FocusFireInterval = 5f;

        private AIDifficulty difficulty;

        public BattleAIBrain(AIDifficulty diff)
        {
            difficulty = diff;
        }

        public void SetDifficulty(AIDifficulty diff)
        {
            difficulty = diff;
        }

        /// <summary>
        /// Called once per decision cycle. Updates phase, allocates forces, issues orders.
        /// </summary>
        public void Think(List<Regiment> enemyRegiments, List<Regiment> playerRegiments, float deltaTime)
        {
            BattleTime += deltaTime;

            // Allocate forces on first call
            if (!forcesAllocated)
            {
                AllocateForces(enemyRegiments);
                forcesAllocated = true;
            }

            // Periodic phase check (not every frame)
            phaseCheckTimer -= deltaTime;
            if (phaseCheckTimer <= 0f)
            {
                phaseCheckTimer = PhaseCheckInterval;
                UpdateBattlePhase(enemyRegiments, playerRegiments);
            }

            // Focus fire target selection (Hard+)
            if (DifficultySettings.Instance != null && DifficultySettings.Instance.AIFocusFire)
            {
                focusFireTimer -= deltaTime;
                if (focusFireTimer <= 0f)
                {
                    focusFireTimer = FocusFireInterval;
                    SelectFocusFireTarget(playerRegiments);
                }
            }

            // Phase-specific coordination (Hard+)
            if (DifficultySettings.Instance != null && DifficultySettings.Instance.AIUsesPhasePlanning)
            {
                ExecutePhaseStrategy(enemyRegiments, playerRegiments);
            }
        }

        /// <summary>Get the tactical role assigned to this regiment.</summary>
        public TacticalRole GetRole(Regiment regiment)
        {
            if (ArtillerySupport.Contains(regiment)) return TacticalRole.ArtillerySupport;
            if (SkirmishScreen.Contains(regiment)) return TacticalRole.SkirmishScreen;
            if (FlankingForce.Contains(regiment)) return TacticalRole.FlankingForce;
            if (Reserve.Contains(regiment)) return TacticalRole.Reserve;
            return TacticalRole.MainLine;
        }

        public bool IsReserveCommitted => reserveCommitted;

        // ================================================================
        // FORCE ALLOCATION
        // ================================================================

        private void AllocateForces(List<Regiment> regiments)
        {
            MainLine.Clear();
            Reserve.Clear();
            FlankingForce.Clear();
            SkirmishScreen.Clear();
            ArtillerySupport.Clear();

            // Categorize by unit type
            List<Regiment> infantry = new List<Regiment>();
            List<Regiment> cavalry = new List<Regiment>();
            List<Regiment> artillery = new List<Regiment>();
            List<Regiment> lightInf = new List<Regiment>();
            List<Regiment> grenadiers = new List<Regiment>();

            foreach (var reg in regiments)
            {
                if (reg == null || reg.CachedAliveCount <= 0 || reg.UnitData == null) continue;

                switch (reg.UnitData.unitType)
                {
                    case UnitType.Artillery:
                        artillery.Add(reg);
                        break;
                    case UnitType.Cavalry:
                    case UnitType.Hussar:
                    case UnitType.Lancer:
                        cavalry.Add(reg);
                        break;
                    case UnitType.LightInfantry:
                        lightInf.Add(reg);
                        break;
                    case UnitType.Grenadier:
                        grenadiers.Add(reg);
                        break;
                    default:
                        infantry.Add(reg);
                        break;
                }
            }

            // Artillery → always in support
            ArtillerySupport.AddRange(artillery);

            // Light infantry → skirmish screen
            SkirmishScreen.AddRange(lightInf);

            // Cavalry → flanking force (if flanking enabled)
            bool canFlank = DifficultySettings.Instance != null && DifficultySettings.Instance.AIFlankingEnabled;
            if (canFlank)
            {
                FlankingForce.AddRange(cavalry);
            }
            else
            {
                MainLine.AddRange(cavalry);
            }

            // Grenadiers → reserve (if reserves enabled and enough troops)
            bool canReserve = DifficultySettings.Instance != null && DifficultySettings.Instance.AIUsesReserves;
            if (canReserve && grenadiers.Count > 0)
            {
                Reserve.AddRange(grenadiers);
            }
            else
            {
                MainLine.AddRange(grenadiers);
            }

            // Remaining infantry → main line, but keep 1 as reserve if we have enough
            if (canReserve && infantry.Count > 3 && Reserve.Count == 0)
            {
                Reserve.Add(infantry[infantry.Count - 1]);
                for (int i = 0; i < infantry.Count - 1; i++)
                    MainLine.Add(infantry[i]);
            }
            else
            {
                MainLine.AddRange(infantry);
            }

            Debug.Log($"[AIBrain] Forces allocated: MainLine={MainLine.Count}, Reserve={Reserve.Count}, " +
                      $"Flanking={FlankingForce.Count}, Skirmish={SkirmishScreen.Count}, Artillery={ArtillerySupport.Count}");
        }

        // ================================================================
        // PHASE DETECTION
        // ================================================================

        private void UpdateBattlePhase(List<Regiment> enemyRegiments, List<Regiment> playerRegiments)
        {
            float ownAlive = CountAlive(enemyRegiments);
            float playerAlive = CountAlive(playerRegiments);
            float totalOwn = CountTotal(enemyRegiments);
            float totalPlayer = CountTotal(playerRegiments);

            if (totalOwn <= 0 || totalPlayer <= 0) return;

            float ownCasualtyRatio = 1f - (ownAlive / Mathf.Max(totalOwn, 1));
            float playerCasualtyRatio = 1f - (playerAlive / Mathf.Max(totalPlayer, 1));
            float avgDistance = GetAverageArmyDistance(enemyRegiments, playerRegiments);

            BattlePhase newPhase = CurrentPhase;

            // Retreat: own casualties > 70%
            if (ownCasualtyRatio > 0.70f && playerCasualtyRatio < 0.50f)
            {
                newPhase = BattlePhase.Retreat;
            }
            // Pursuit: enemy casualties > 60%
            else if (playerCasualtyRatio > 0.60f)
            {
                newPhase = BattlePhase.Pursuit;
            }
            // Crisis: own casualties > 40% or significantly losing
            else if (ownCasualtyRatio > 0.40f || (ownCasualtyRatio > playerCasualtyRatio + 0.15f))
            {
                newPhase = BattlePhase.Crisis;
            }
            // Engagement: armies within 100m
            else if (avgDistance < 100f || BattleTime > 30f)
            {
                newPhase = BattlePhase.Engagement;
            }
            // Opening: initial phase
            else
            {
                newPhase = BattlePhase.Opening;
            }

            if (newPhase != CurrentPhase)
            {
                Debug.Log($"[AIBrain] Phase transition: {CurrentPhase} → {newPhase} " +
                          $"(ownCas={ownCasualtyRatio:P0}, playerCas={playerCasualtyRatio:P0}, dist={avgDistance:F0}m)");
                CurrentPhase = newPhase;
            }
        }

        // ================================================================
        // PHASE STRATEGY
        // ================================================================

        private void ExecutePhaseStrategy(List<Regiment> enemyRegiments, List<Regiment> playerRegiments)
        {
            switch (CurrentPhase)
            {
                case BattlePhase.Opening:
                    // Position forces on advantageous terrain during opening
                    PositionForcesOnTerrain(playerRegiments);
                    break;

                case BattlePhase.Engagement:
                    // Main line forms line, cavalry holds flanks
                    break;

                case BattlePhase.Crisis:
                    // Commit reserves
                    if (!reserveCommitted && Reserve.Count > 0)
                    {
                        CommitReserves();
                    }
                    break;

                case BattlePhase.Pursuit:
                    // Cavalry pursues, infantry advances aggressively
                    if (!reserveCommitted && Reserve.Count > 0)
                    {
                        CommitReserves();
                    }
                    break;

                case BattlePhase.Retreat:
                    // Everyone falls back
                    break;
            }
        }

        private void CommitReserves()
        {
            reserveCommitted = true;
            // Move reserve regiments to main line
            foreach (var reg in Reserve)
            {
                if (!MainLine.Contains(reg))
                    MainLine.Add(reg);
            }
            Debug.Log($"[AIBrain] RESERVE COMMITTED! {Reserve.Count} regiments joining the fight!");
        }

        // ================================================================
        // FOCUS FIRE
        // ================================================================

        private void SelectFocusFireTarget(List<Regiment> playerRegiments)
        {
            if (playerRegiments == null) return;

            Regiment weakest = null;
            float lowestScore = float.MaxValue;

            foreach (var reg in playerRegiments)
            {
                if (reg == null || reg.CachedAliveCount <= 0) continue;

                // Score: lower is better target (fewer alive + lower morale = easier kill)
                float aliveRatio = (float)reg.CachedAliveCount / Mathf.Max(reg.Units.Count, 1);
                float moraleScore = reg.AverageMorale / 100f;
                float score = aliveRatio * 0.6f + moraleScore * 0.4f;

                // Bonus for already engaged/damaged units
                if (aliveRatio < 0.5f) score -= 0.2f;

                if (score < lowestScore)
                {
                    lowestScore = score;
                    weakest = reg;
                }
            }

            FocusFireTarget = weakest;
        }

        // ================================================================
        // UTILITY
        // ================================================================

        private float CountAlive(List<Regiment> regiments)
        {
            int total = 0;
            if (regiments == null) return 0;
            foreach (var r in regiments)
                if (r != null) total += r.CachedAliveCount;
            return total;
        }

        private float CountTotal(List<Regiment> regiments)
        {
            int total = 0;
            if (regiments == null) return 0;
            foreach (var r in regiments)
                if (r != null) total += r.Units.Count;
            return total;
        }

        // ================================================================
        // TERRAIN AWARENESS
        // ================================================================

        /// <summary>
        /// Scans the battlefield for high ground positions and returns the best
        /// deployment position for a regiment based on its role.
        /// </summary>
        public Vector3 FindBestTerrainPosition(Regiment regiment, List<Regiment> playerRegiments)
        {
            if (regiment == null) return Vector3.zero;

            Vector3 currentPos = regiment.transform.position;
            Vector3 bestPos = currentPos;
            float bestScore = EvaluateTerrainScore(currentPos, regiment, playerRegiments);

            // Sample positions in a grid around the current position
            float searchRadius = 80f;
            float step = 20f;

            for (float dx = -searchRadius; dx <= searchRadius; dx += step)
            {
                for (float dz = -searchRadius; dz <= searchRadius; dz += step)
                {
                    Vector3 candidate = currentPos + new Vector3(dx, 0f, dz);
                    candidate.y = BattleManager.GetTerrainHeight(candidate);

                    float score = EvaluateTerrainScore(candidate, regiment, playerRegiments);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = candidate;
                    }
                }
            }

            return bestPos;
        }

        /// <summary>
        /// Scores a position for a regiment. Higher = better.
        /// Factors: height advantage, distance to enemy, terrain type.
        /// </summary>
        private float EvaluateTerrainScore(Vector3 position, Regiment regiment, List<Regiment> playerRegiments)
        {
            float score = 0f;

            // Height advantage is very valuable
            float height = position.y;
            score += height * 2f; // 2 points per meter of elevation

            // Get average enemy position
            Vector3 enemyCenter = Vector3.zero;
            int enemyCount = 0;
            if (playerRegiments != null)
            {
                foreach (var r in playerRegiments)
                {
                    if (r != null && r.CachedAliveCount > 0)
                    {
                        enemyCenter += r.transform.position;
                        enemyCount++;
                    }
                }
            }
            if (enemyCount > 0) enemyCenter /= enemyCount;

            float distToEnemy = Vector3.Distance(position, enemyCenter);

            // Artillery prefers high ground at range
            if (regiment.IsArtillery)
            {
                score += height * 3f; // Extra height bonus for artillery
                // Prefer positions at medium-long range (100-200m)
                if (distToEnemy > 80f && distToEnemy < 220f)
                    score += 20f;
                else if (distToEnemy < 50f)
                    score -= 30f; // Too close is dangerous for artillery
            }
            // Cavalry prefers flat ground and flanking positions
            else if (regiment.UnitData != null && regiment.UnitData.canCharge)
            {
                // Cavalry doesn't want steep hills — check nearby height variance
                score -= Mathf.Abs(height - BattleManager.GetTerrainHeight(position + Vector3.forward * 10f)) * 5f;

                // Prefer flanking positions (perpendicular to the enemy center axis)
                if (enemyCount > 0)
                {
                    Vector3 toEnemy = (enemyCenter - position).normalized;
                    // Score lateral offset from the direct approach
                    Vector3 lateral = Vector3.Cross(toEnemy, Vector3.up);
                    float lateralOffset = Mathf.Abs(Vector3.Dot(position - enemyCenter, lateral));
                    score += lateralOffset * 0.3f; // Prefer flanking angles
                }
            }
            // Infantry prefers defensive high ground
            else
            {
                score += height * 1.5f;
                // Prefer positions facing the enemy at medium range
                if (distToEnemy > 50f && distToEnemy < 150f)
                    score += 10f;
            }

            return score;
        }

        /// <summary>
        /// Called during Opening phase to position forces on advantageous terrain.
        /// </summary>
        public void PositionForcesOnTerrain(List<Regiment> playerRegiments)
        {
            // Position artillery on highest available ground
            foreach (var reg in ArtillerySupport)
            {
                if (reg == null || reg.CachedAliveCount <= 0) continue;
                Vector3 bestPos = FindBestTerrainPosition(reg, playerRegiments);
                if (Vector3.Distance(bestPos, reg.transform.position) > 10f)
                {
                    reg.MoveRegiment(bestPos);
                }
            }

            // Position main line on elevated terrain
            foreach (var reg in MainLine)
            {
                if (reg == null || reg.CachedAliveCount <= 0) continue;
                Vector3 bestPos = FindBestTerrainPosition(reg, playerRegiments);
                if (Vector3.Distance(bestPos, reg.transform.position) > 15f)
                {
                    reg.MoveRegiment(bestPos);
                }
            }

            // Cavalry to flanking positions
            foreach (var reg in FlankingForce)
            {
                if (reg == null || reg.CachedAliveCount <= 0) continue;
                Vector3 bestPos = FindBestTerrainPosition(reg, playerRegiments);
                if (Vector3.Distance(bestPos, reg.transform.position) > 15f)
                {
                    reg.MoveRegiment(bestPos);
                }
            }
        }

        private float GetAverageArmyDistance(List<Regiment> army1, List<Regiment> army2)
        {
            if (army1 == null || army2 == null || army1.Count == 0 || army2.Count == 0)
                return 999f;

            // Use center of mass of each army
            Vector3 center1 = Vector3.zero;
            Vector3 center2 = Vector3.zero;
            int count1 = 0, count2 = 0;

            foreach (var r in army1)
            {
                if (r != null && r.CachedAliveCount > 0)
                {
                    center1 += r.transform.position;
                    count1++;
                }
            }
            foreach (var r in army2)
            {
                if (r != null && r.CachedAliveCount > 0)
                {
                    center2 += r.transform.position;
                    count2++;
                }
            }

            if (count1 == 0 || count2 == 0) return 999f;

            center1 /= count1;
            center2 /= count2;
            return Vector3.Distance(center1, center2);
        }
    }
}
