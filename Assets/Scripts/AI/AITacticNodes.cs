using UnityEngine;
using NapoleonicWars.Units;
using NapoleonicWars.Data;
using NapoleonicWars.Core;
using System.Collections.Generic;

namespace NapoleonicWars.AI
{
    /// <summary>
    /// Reusable Behavior Tree leaf nodes for Napoleonic tactical AI.
    /// Includes basic conditions/actions and advanced difficulty-gated tactics.
    /// </summary>
    public static class AITacticNodes
    {
        // ============================================================
        // CONDITIONS — BASIC
        // ============================================================

        public static Condition HasAliveUnits()
        {
            return new Condition("HasAliveUnits", ctx => ctx.Regiment != null && ctx.Regiment.CachedAliveCount > 0);
        }

        public static Condition HasTarget()
        {
            return new Condition("HasTarget", ctx => ctx.TargetRegiment != null && ctx.TargetRegiment.CachedAliveCount > 0);
        }

        public static Condition IsInRange(float range)
        {
            return new Condition($"IsInRange({range})", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return false;
                float distSqr = (ctx.Regiment.transform.position - ctx.TargetRegiment.transform.position).sqrMagnitude;
                return distSqr <= range * range;
            });
        }

        public static Condition IsOutOfRange(float range)
        {
            return new Condition($"IsOutOfRange({range})", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return true;
                float distSqr = (ctx.Regiment.transform.position - ctx.TargetRegiment.transform.position).sqrMagnitude;
                return distSqr > range * range;
            });
        }

        public static Condition MoraleAbove(float threshold)
        {
            return new Condition($"MoraleAbove({threshold})", ctx =>
                ctx.Regiment != null && ctx.Regiment.CachedAverageMorale > threshold);
        }

        public static Condition MoraleBelow(float threshold)
        {
            return new Condition($"MoraleBelow({threshold})", ctx =>
                ctx.Regiment != null && ctx.Regiment.CachedAverageMorale < threshold);
        }

        public static Condition StaminaAbove(float percent)
        {
            return new Condition($"StaminaAbove({percent})", ctx =>
                ctx.Regiment != null && ctx.Regiment.CachedAverageStamina > percent);
        }

        public static Condition HasAmmo()
        {
            return new Condition("HasAmmo", ctx =>
                ctx.Regiment != null && ctx.Regiment.CachedTotalAmmo > 0);
        }

        public static Condition EnemyIsFlankable()
        {
            return new Condition("EnemyIsFlankable", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return false;
                if (ctx.TargetRegiment.IsInSquareFormation()) return false;

                Vector3 toUs = (ctx.Regiment.transform.position - ctx.TargetRegiment.transform.position).normalized;
                float dot = Vector3.Dot(ctx.TargetRegiment.transform.forward, toUs);
                return dot < 0.3f;
            });
        }

        public static Condition EnemyInSquare()
        {
            return new Condition("EnemyInSquare", ctx =>
                ctx.TargetRegiment != null && ctx.TargetRegiment.IsInSquareFormation());
        }

        public static Condition IsUnitType(params UnitType[] types)
        {
            return new Condition("IsUnitType", ctx =>
            {
                if (ctx.Regiment == null || ctx.Regiment.UnitData == null) return false;
                foreach (var t in types)
                    if (ctx.Regiment.UnitData.unitType == t) return true;
                return false;
            });
        }

        public static Condition TargetIsUnitType(params UnitType[] types)
        {
            return new Condition("TargetIsUnitType", ctx =>
            {
                if (ctx.TargetRegiment == null || ctx.TargetRegiment.UnitData == null) return false;
                foreach (var t in types)
                    if (ctx.TargetRegiment.UnitData.unitType == t) return true;
                return false;
            });
        }

        public static Condition DifficultyAtLeast(AIDifficulty minDifficulty)
        {
            return new Condition($"DifficultyAtLeast({minDifficulty})", ctx =>
                ctx.Difficulty >= minDifficulty);
        }

        public static Condition OfficerAlive()
        {
            return new Condition("OfficerAlive", ctx =>
                ctx.Regiment != null && ctx.Regiment.OfficerAlive);
        }

        public static Condition EnemyMoraleBelow(float threshold)
        {
            return new Condition($"EnemyMoraleBelow({threshold})", ctx =>
                ctx.TargetRegiment != null && ctx.TargetRegiment.CachedAverageMorale < threshold);
        }

        // ============================================================
        // CONDITIONS — ADVANCED (phase / role awareness)
        // ============================================================

        public static Condition PhaseIs(BattlePhase phase)
        {
            return new Condition($"PhaseIs({phase})", ctx => ctx.Phase == phase);
        }

        public static Condition RoleIs(TacticalRole role)
        {
            return new Condition($"RoleIs({role})", ctx => ctx.Role == role);
        }

        public static Condition EnemyIsFlankingUs()
        {
            return new Condition("EnemyIsFlankingUs", ctx =>
            {
                if (ctx.Regiment == null || ctx.BattleManager == null) return false;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null) return false;

                foreach (var reg in playerRegs)
                {
                    if (reg.CachedAliveCount <= 0) continue;
                    Vector3 toEnemy = (reg.transform.position - ctx.Regiment.transform.position).normalized;
                    float dot = Vector3.Dot(ctx.Regiment.transform.forward, toEnemy);
                    if (dot < 0.0f)
                    {
                        float dist = Vector3.Distance(reg.transform.position, ctx.Regiment.transform.position);
                        if (dist < 80f) return true;
                    }
                }
                return false;
            });
        }

        // ============================================================
        // ACTIONS — BASIC
        // ============================================================

        public static BTAction FindNearestEnemy()
        {
            return new BTAction("FindNearestEnemy", ctx =>
            {
                if (ctx.BattleManager == null) return BTStatus.Failure;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null || playerRegs.Count == 0) return BTStatus.Failure;

                Regiment nearest = null;
                float nearestDistSqr = float.MaxValue;

                foreach (var reg in playerRegs)
                {
                    if (reg.CachedAliveCount <= 0) continue;
                    float distSqr = (ctx.Regiment.transform.position - reg.transform.position).sqrMagnitude;
                    if (distSqr < nearestDistSqr)
                    {
                        nearestDistSqr = distSqr;
                        nearest = reg;
                    }
                }

                if (nearest != null)
                {
                    ctx.TargetRegiment = nearest;
                    return BTStatus.Success;
                }
                return BTStatus.Failure;
            });
        }

        public static BTAction FindBestTarget()
        {
            return new BTAction("FindBestTarget", ctx =>
            {
                if (ctx.BattleManager == null) return BTStatus.Failure;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null) return BTStatus.Failure;

                Regiment best = null;
                float bestScore = float.MinValue;
                var myType = ctx.Regiment.UnitData?.unitType ?? UnitType.LineInfantry;

                foreach (var reg in playerRegs)
                {
                    if (reg.CachedAliveCount <= 0) continue;
                    float dist = Vector3.Distance(ctx.Regiment.transform.position, reg.transform.position);
                    float score = 0f;

                    score += (100f - reg.CachedAverageMorale) * 0.3f;

                    if (myType == UnitType.Cavalry || myType == UnitType.Hussar || myType == UnitType.Lancer)
                    {
                        if (reg.UnitData?.unitType == UnitType.Artillery) score += 50f;
                        if (reg.IsInSquareFormation()) score -= 80f;
                    }

                    if (myType == UnitType.Artillery)
                        score += reg.CachedAliveCount * 0.5f;

                    score -= dist * 0.15f;
                    if (reg.CachedTotalAmmo <= 0) score += 20f;
                    score += (1f - reg.CachedAverageStamina) * 15f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = reg;
                    }
                }

                if (best != null)
                {
                    ctx.TargetRegiment = best;
                    return BTStatus.Success;
                }
                return BTStatus.Failure;
            });
        }

        public static BTAction SetFormation(FormationType formation)
        {
            return new BTAction($"SetFormation({formation})", ctx =>
            {
                if (ctx.Regiment == null) return BTStatus.Failure;
                if (ctx.Regiment.CurrentFormation != formation)
                    ctx.Regiment.SetFormation(formation);
                return BTStatus.Success;
            });
        }

        public static BTAction Attack()
        {
            return new BTAction("Attack", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;
                ctx.Regiment.AttackTargetRegiment(ctx.TargetRegiment);
                return BTStatus.Success;
            });
        }

        public static BTAction Charge()
        {
            return new BTAction("Charge", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;
                ctx.Regiment.ChargeTargetRegiment(ctx.TargetRegiment);
                return BTStatus.Success;
            });
        }

        public static BTAction AdvanceToward(float distance)
        {
            return new BTAction($"AdvanceToward({distance})", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;
                Vector3 dir = (ctx.TargetRegiment.transform.position - ctx.Regiment.transform.position).normalized;
                Vector3 dest = ctx.Regiment.transform.position + dir * distance;
                ctx.Regiment.MoveRegiment(dest);
                return BTStatus.Success;
            });
        }

        public static BTAction Retreat(float distance)
        {
            return new BTAction($"Retreat({distance})", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;
                Vector3 dir = (ctx.Regiment.transform.position - ctx.TargetRegiment.transform.position).normalized;
                Vector3 dest = ctx.Regiment.transform.position + dir * distance;
                ctx.Regiment.MoveRegiment(dest);
                return BTStatus.Success;
            });
        }

        public static BTAction FaceEnemy()
        {
            return new BTAction("FaceEnemy", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;
                Vector3 dir = ctx.TargetRegiment.transform.position - ctx.Regiment.transform.position;
                dir.y = 0f;
                ctx.Regiment.RotateRegiment(dir);
                return BTStatus.Success;
            });
        }

        public static BTAction SetVolleyMode(bool enabled)
        {
            return new BTAction($"SetVolleyMode({enabled})", ctx =>
            {
                if (ctx.Regiment == null) return BTStatus.Failure;
                ctx.Regiment.SetVolleyMode(enabled);
                return BTStatus.Success;
            });
        }

        public static BTAction MoveToFlank()
        {
            return new BTAction("MoveToFlank", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;

                Vector3 targetRight = ctx.TargetRegiment.transform.right;
                Vector3 toUs = ctx.Regiment.transform.position - ctx.TargetRegiment.transform.position;
                float side = Vector3.Dot(targetRight, toUs) > 0 ? 1f : -1f;

                Vector3 flankPos = ctx.TargetRegiment.transform.position + targetRight * side * 25f;
                flankPos -= ctx.TargetRegiment.transform.forward * 10f;

                ctx.Regiment.MoveRegiment(flankPos);
                return BTStatus.Success;
            });
        }

        public static BTAction MoveToReservePosition()
        {
            return new BTAction("MoveToReserve", ctx =>
            {
                if (ctx.Regiment == null || ctx.BattleManager == null) return BTStatus.Failure;

                Vector3 center = Vector3.zero;
                int count = 0;
                foreach (var reg in ctx.BattleManager.EnemyRegiments)
                {
                    if (reg.CachedAliveCount > 0)
                    {
                        center += reg.transform.position;
                        count++;
                    }
                }
                if (count == 0) return BTStatus.Failure;
                center /= count;

                Vector3 reservePos = center + Vector3.forward * 30f;
                ctx.Regiment.MoveRegiment(reservePos);
                return BTStatus.Success;
            });
        }

        // ============================================================
        // ACTIONS — ADVANCED (Hard+ / Legendary)
        // ============================================================

        /// <summary>Feigned retreat: pull back, then charge when enemy follows (Legendary)</summary>
        public static BTAction FeintRetreat()
        {
            return new BTAction("FeintRetreat", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;

                bool hasFeinted = ctx.Get("feinted", false);
                float feintTimer = ctx.Get("feintTimer", 0f);

                if (!hasFeinted)
                {
                    Vector3 dir = (ctx.Regiment.transform.position - ctx.TargetRegiment.transform.position).normalized;
                    Vector3 retreatPos = ctx.Regiment.transform.position + dir * 25f;
                    ctx.Regiment.MoveRegiment(retreatPos);
                    ctx.Set("feinted", true);
                    ctx.Set("feintTimer", 0f);
                    return BTStatus.Running;
                }
                else
                {
                    feintTimer += ctx.DeltaTime;
                    ctx.Set("feintTimer", feintTimer);

                    float dist = Vector3.Distance(ctx.Regiment.transform.position, ctx.TargetRegiment.transform.position);
                    if (feintTimer > 4f || dist < 20f)
                    {
                        ctx.Regiment.ChargeTargetRegiment(ctx.TargetRegiment);
                        ctx.Set("feinted", false);
                        return BTStatus.Success;
                    }
                    return BTStatus.Running;
                }
            });
        }

        /// <summary>Pursue routing enemies aggressively (Hard+)</summary>
        public static BTAction PursueRoutingEnemy()
        {
            return new BTAction("PursueRouting", ctx =>
            {
                if (ctx.BattleManager == null) return BTStatus.Failure;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null) return BTStatus.Failure;

                Regiment routing = null;
                float bestScore = float.MinValue;

                foreach (var reg in playerRegs)
                {
                    if (reg.CachedAliveCount <= 0) continue;
                    if (reg.CachedAverageMorale > 35f) continue;

                    float dist = Vector3.Distance(ctx.Regiment.transform.position, reg.transform.position);
                    float score = (35f - reg.CachedAverageMorale) - dist * 0.1f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        routing = reg;
                    }
                }

                if (routing != null)
                {
                    ctx.TargetRegiment = routing;
                    ctx.Regiment.ChargeTargetRegiment(routing);
                    return BTStatus.Success;
                }
                return BTStatus.Failure;
            });
        }

        /// <summary>Counter-flank: detect flanking and reposition to block (Legendary)</summary>
        public static BTAction CounterFlank()
        {
            return new BTAction("CounterFlank", ctx =>
            {
                if (ctx.Regiment == null || ctx.BattleManager == null) return BTStatus.Failure;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null) return BTStatus.Failure;

                Regiment flanker = null;
                float worstDot = 1f;

                foreach (var reg in playerRegs)
                {
                    if (reg.CachedAliveCount <= 0) continue;
                    Vector3 toEnemy = (reg.transform.position - ctx.Regiment.transform.position).normalized;
                    float dot = Vector3.Dot(ctx.Regiment.transform.forward, toEnemy);
                    if (dot < worstDot && dot < 0.0f)
                    {
                        float dist = Vector3.Distance(reg.transform.position, ctx.Regiment.transform.position);
                        if (dist < 80f)
                        {
                            worstDot = dot;
                            flanker = reg;
                        }
                    }
                }

                if (flanker != null)
                {
                    ctx.TargetRegiment = flanker;
                    ctx.Regiment.SetFormation(FormationType.Square);
                    Vector3 dir = flanker.transform.position - ctx.Regiment.transform.position;
                    dir.y = 0f;
                    ctx.Regiment.RotateRegiment(dir);
                    return BTStatus.Success;
                }
                return BTStatus.Failure;
            });
        }

        /// <summary>Focus fire on the brain's selected weakest target (Hard+)</summary>
        public static BTAction FindFocusFireTarget(BattleAIBrain brain)
        {
            return new BTAction("FindFocusFireTarget", ctx =>
            {
                if (brain == null || brain.FocusFireTarget == null) return BTStatus.Failure;
                if (brain.FocusFireTarget.CachedAliveCount <= 0) return BTStatus.Failure;
                ctx.TargetRegiment = brain.FocusFireTarget;
                return BTStatus.Success;
            });
        }

        /// <summary>Orderly withdrawal while maintaining formation (Hard+)</summary>
        public static BTAction WithdrawUnderFire()
        {
            return new BTAction("WithdrawUnderFire", ctx =>
            {
                if (ctx.Regiment == null) return BTStatus.Failure;

                Vector3 enemyCenter = Vector3.zero;
                int count = 0;
                if (ctx.BattleManager?.PlayerRegiments != null)
                {
                    foreach (var r in ctx.BattleManager.PlayerRegiments)
                    {
                        if (r.CachedAliveCount > 0)
                        {
                            enemyCenter += r.transform.position;
                            count++;
                        }
                    }
                }
                if (count == 0) return BTStatus.Failure;
                enemyCenter /= count;

                Vector3 retreatDir = (ctx.Regiment.transform.position - enemyCenter).normalized;
                Vector3 dest = ctx.Regiment.transform.position + retreatDir * 15f;
                ctx.Regiment.MoveRegiment(dest);
                ctx.Regiment.RotateRegiment(-retreatDir);
                ctx.Regiment.SetFormation(FormationType.Line);
                return BTStatus.Success;
            });
        }

        /// <summary>Wait at position until enemy enters optimal volley range (Normal+)</summary>
        public static BTAction WaitForVolleyRange()
        {
            return new BTAction("WaitForVolleyRange", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;
                float dist = Vector3.Distance(ctx.Regiment.transform.position, ctx.TargetRegiment.transform.position);

                if (dist > 30f && dist < 70f)
                {
                    ctx.Regiment.SetFormation(FormationType.Line);
                    ctx.Regiment.SetVolleyMode(true);
                    Vector3 dir = ctx.TargetRegiment.transform.position - ctx.Regiment.transform.position;
                    dir.y = 0f;
                    ctx.Regiment.RotateRegiment(dir);
                    ctx.Regiment.AttackTargetRegiment(ctx.TargetRegiment);
                    return BTStatus.Success;
                }
                else if (dist >= 70f && dist < 120f)
                {
                    ctx.Regiment.SetFormation(FormationType.Line);
                    Vector3 dir = ctx.TargetRegiment.transform.position - ctx.Regiment.transform.position;
                    dir.y = 0f;
                    ctx.Regiment.RotateRegiment(dir);
                    return BTStatus.Running;
                }
                return BTStatus.Failure;
            });
        }

        /// <summary>Skirmish screen: harass and slow enemy advance (Normal+)</summary>
        public static BTAction ScreenAndDelay()
        {
            return new BTAction("ScreenAndDelay", ctx =>
            {
                if (ctx.Regiment == null || ctx.TargetRegiment == null) return BTStatus.Failure;
                if (ctx.Regiment.CachedTotalAmmo <= 0) return BTStatus.Failure;

                float dist = Vector3.Distance(ctx.Regiment.transform.position, ctx.TargetRegiment.transform.position);

                if (dist < 25f)
                {
                    Vector3 retreatDir = (ctx.Regiment.transform.position - ctx.TargetRegiment.transform.position).normalized;
                    ctx.Regiment.MoveRegiment(ctx.Regiment.transform.position + retreatDir * 20f);
                    ctx.Regiment.AttackTargetRegiment(ctx.TargetRegiment);
                    return BTStatus.Success;
                }
                else if (dist < 55f)
                {
                    ctx.Regiment.SetFormation(FormationType.Skirmish);
                    Vector3 dir = ctx.TargetRegiment.transform.position - ctx.Regiment.transform.position;
                    dir.y = 0f;
                    ctx.Regiment.RotateRegiment(dir);
                    ctx.Regiment.AttackTargetRegiment(ctx.TargetRegiment);
                    return BTStatus.Success;
                }
                else
                {
                    Vector3 dir = (ctx.TargetRegiment.transform.position - ctx.Regiment.transform.position).normalized;
                    ctx.Regiment.MoveRegiment(ctx.Regiment.transform.position + dir * 15f);
                    return BTStatus.Running;
                }
            });
        }

        // ============================================================
        // CONDITIONS — FORMATION REACTION
        // ============================================================

        /// <summary>True if any nearby enemy cavalry is within charge range.</summary>
        public static Condition EnemyCavalryNearby(float range = 80f)
        {
            return new Condition($"EnemyCavalryNearby({range})", ctx =>
            {
                if (ctx.BattleManager == null) return false;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null) return false;

                foreach (var reg in playerRegs)
                {
                    if (reg == null || reg.CachedAliveCount <= 0 || reg.UnitData == null) continue;
                    if (!reg.UnitData.canCharge) continue; // Only cavalry with charge
                    float dist = Vector3.Distance(ctx.Regiment.transform.position, reg.transform.position);
                    if (dist < range) return true;
                }
                return false;
            });
        }

        /// <summary>True if the target enemy regiment is using skirmish formation.</summary>
        public static Condition EnemyIsSkirmishing()
        {
            return new Condition("EnemyIsSkirmishing", ctx =>
                ctx.TargetRegiment != null && ctx.TargetRegiment.IsSkirmishing);
        }

        /// <summary>True if target has high suppression (vulnerable to charge).</summary>
        public static Condition EnemyIsSuppressed(float threshold = 40f)
        {
            return new Condition($"EnemyIsSuppressed({threshold})", ctx =>
                ctx.TargetRegiment != null && ctx.TargetRegiment.CachedAverageSuppression > threshold);
        }

        /// <summary>True if target artillery is unlimbered (stationary, vulnerable).</summary>
        public static Condition EnemyArtilleryIsStationary()
        {
            return new Condition("EnemyArtilleryIsStationary", ctx =>
            {
                if (ctx.TargetRegiment == null) return false;
                return ctx.TargetRegiment.IsArtillery && !ctx.TargetRegiment.IsLimbered;
            });
        }

        // ============================================================
        // ACTIONS — FORMATION REACTION
        // ============================================================

        /// <summary>Form square to counter nearby cavalry (all difficulties).</summary>
        public static BTAction FormSquareAgainstCavalry()
        {
            return new BTAction("FormSquareAgainstCavalry", ctx =>
            {
                if (ctx.Regiment == null) return BTStatus.Failure;
                ctx.Regiment.SetFormation(FormationType.Square);
                return BTStatus.Success;
            });
        }

        /// <summary>Concentrate fire on skirmishers to punch through the screen.</summary>
        public static BTAction ConcentrateFireOnSkirmishers()
        {
            return new BTAction("ConcentrateFireOnSkirmishers", ctx =>
            {
                if (ctx.BattleManager == null || ctx.Regiment == null) return BTStatus.Failure;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null) return BTStatus.Failure;

                // Find nearest skirmishing enemy
                Regiment nearestSkirmisher = null;
                float nearestDist = float.MaxValue;
                foreach (var reg in playerRegs)
                {
                    if (reg == null || reg.CachedAliveCount <= 0) continue;
                    if (!reg.IsSkirmishing) continue;
                    float dist = Vector3.Distance(ctx.Regiment.transform.position, reg.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestSkirmisher = reg;
                    }
                }

                if (nearestSkirmisher != null && nearestDist < 100f)
                {
                    ctx.TargetRegiment = nearestSkirmisher;
                    ctx.Regiment.SetVolleyMode(true);
                    ctx.Regiment.AttackTargetRegiment(nearestSkirmisher);
                    return BTStatus.Success;
                }
                return BTStatus.Failure;
            });
        }

        /// <summary>Send cavalry to flank and destroy stationary artillery.</summary>
        public static BTAction HuntEnemyArtillery()
        {
            return new BTAction("HuntEnemyArtillery", ctx =>
            {
                if (ctx.BattleManager == null || ctx.Regiment == null) return BTStatus.Failure;
                var playerRegs = ctx.BattleManager.PlayerRegiments;
                if (playerRegs == null) return BTStatus.Failure;

                // Find nearest enemy artillery
                Regiment nearestArtillery = null;
                float nearestDist = float.MaxValue;
                foreach (var reg in playerRegs)
                {
                    if (reg == null || reg.CachedAliveCount <= 0) continue;
                    if (!reg.IsArtillery) continue;
                    float dist = Vector3.Distance(ctx.Regiment.transform.position, reg.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestArtillery = reg;
                    }
                }

                if (nearestArtillery != null)
                {
                    ctx.TargetRegiment = nearestArtillery;
                    ctx.Regiment.ChargeTargetRegiment(nearestArtillery);
                    return BTStatus.Success;
                }
                return BTStatus.Failure;
            });
        }

        // ============================================================
        // COMPOSITE TACTICS — Difficulty-gated
        // ============================================================

        public static BTNode InfantryLineTactic()
        {
            return new Selector("InfantryLineTactic",
                // RETREAT PHASE: orderly withdrawal (Hard+)
                new Sequence("PhaseRetreat",
                    PhaseIs(BattlePhase.Retreat),
                    DifficultyAtLeast(AIDifficulty.Hard),
                    HasTarget(),
                    WithdrawUnderFire()
                ),
                // COUNTER-FLANKING (Legendary)
                new Sequence("CounterFlankCheck",
                    DifficultyAtLeast(AIDifficulty.Legendary),
                    EnemyIsFlankingUs(),
                    CounterFlank()
                ),
                // ANTI-CAVALRY: form square when enemy cavalry is near (Normal+)
                new Sequence("AntiCavalrySquare",
                    DifficultyAtLeast(AIDifficulty.Normal),
                    EnemyCavalryNearby(60f),
                    FormSquareAgainstCavalry()
                ),
                // ANTI-SKIRMISHER: concentrate fire on skirmish screens (Hard+)
                new Sequence("AntiSkirmisher",
                    DifficultyAtLeast(AIDifficulty.Hard),
                    HasTarget(),
                    EnemyIsSkirmishing(),
                    ConcentrateFireOnSkirmishers()
                ),
                // CRISIS: form square
                new Sequence("CrisisSquare",
                    MoraleBelow(40f),
                    SetFormation(FormationType.Square)
                ),
                // HARD+: Wait at optimal volley range
                new Sequence("WaitVolley",
                    DifficultyAtLeast(AIDifficulty.Hard),
                    HasTarget(),
                    IsOutOfRange(30f),
                    IsInRange(120f),
                    WaitForVolleyRange()
                ),
                // ENGAGE: form line and volley
                new Sequence("EngageVolley",
                    HasTarget(),
                    IsInRange(70f),
                    SetFormation(FormationType.Line),
                    SetVolleyMode(true),
                    FaceEnemy(),
                    Attack()
                ),
                // ADVANCE in column
                new Sequence("AdvanceColumn",
                    HasTarget(),
                    IsOutOfRange(70f),
                    SetFormation(FormationType.Column),
                    AdvanceToward(15f)
                ),
                FindNearestEnemy()
            );
        }

        public static BTNode CavalryTactic()
        {
            return new Selector("CavalryTactic",
                // PURSUIT (Hard+)
                new Sequence("PursuitMode",
                    DifficultyAtLeast(AIDifficulty.Hard),
                    PhaseIs(BattlePhase.Pursuit),
                    PursueRoutingEnemy()
                ),
                // HUNT ARTILLERY: prioritize destroying enemy guns (Hard+)
                new Sequence("HuntArtillery",
                    DifficultyAtLeast(AIDifficulty.Hard),
                    StaminaAbove(0.5f),
                    MoraleAbove(50f),
                    HuntEnemyArtillery()
                ),
                // EXPLOIT SUPPRESSION: charge suppressed enemies (Normal+)
                new Sequence("ExploitSuppression",
                    DifficultyAtLeast(AIDifficulty.Normal),
                    HasTarget(),
                    EnemyIsSuppressed(40f),
                    IsInRange(60f),
                    StaminaAbove(0.3f),
                    Charge()
                ),
                // FEIGNED RETREAT (Legendary)
                new Sequence("FeintSequence",
                    DifficultyAtLeast(AIDifficulty.Legendary),
                    HasTarget(),
                    IsInRange(40f),
                    MoraleAbove(60f),
                    StaminaAbove(0.6f),
                    FeintRetreat()
                ),
                // Flank and charge
                new Sequence("FlankAndCharge",
                    HasTarget(),
                    EnemyIsFlankable(),
                    StaminaAbove(0.4f),
                    MoveToFlank(),
                    new Sequence("ChargeWhenClose",
                        IsInRange(15f),
                        Charge()
                    )
                ),
                // Avoid square
                new Sequence("AvoidSquare",
                    HasTarget(),
                    EnemyInSquare(),
                    FindBestTarget()
                ),
                // Direct charge
                new Sequence("DirectCharge",
                    HasTarget(),
                    IsInRange(30f),
                    EnemyMoraleBelow(60f),
                    StaminaAbove(0.3f),
                    Charge()
                ),
                new Sequence("CavalryAdvance",
                    HasTarget(),
                    AdvanceToward(20f)
                ),
                FindBestTarget(),
                FindNearestEnemy()
            );
        }

        public static BTNode ArtilleryTactic()
        {
            return ArtilleryTacticWithBrain(null);
        }

        public static BTNode ArtilleryTacticWithBrain(BattleAIBrain brain)
        {
            var nodes = new List<BTNode>();

            nodes.Add(new Sequence("ArtilleryRetreat",
                HasTarget(), IsInRange(20f), Retreat(30f)));

            if (brain != null)
            {
                nodes.Add(new Sequence("ArtilleryFocusFire",
                    DifficultyAtLeast(AIDifficulty.Hard),
                    FindFocusFireTarget(brain),
                    IsInRange(120f), FaceEnemy(), Attack()));
            }

            nodes.Add(new Sequence("ArtilleryFire",
                HasTarget(), IsInRange(120f), FaceEnemy(), Attack()));

            nodes.Add(new Sequence("ArtilleryAdvance",
                HasTarget(), IsOutOfRange(120f), AdvanceToward(10f)));

            nodes.Add(FindBestTarget());
            nodes.Add(FindNearestEnemy());

            return new Selector("ArtilleryTactic", nodes.ToArray());
        }

        public static BTNode SkirmisherTactic()
        {
            return new Selector("SkirmisherTactic",
                new Sequence("NoAmmoRetreat",
                    new Inverter("HasNoAmmo", HasAmmo()),
                    Retreat(20f)
                ),
                // SCREEN AND DELAY (Normal+)
                new Sequence("ScreenDelay",
                    DifficultyAtLeast(AIDifficulty.Normal),
                    PhaseIs(BattlePhase.Opening),
                    HasTarget(),
                    ScreenAndDelay()
                ),
                new Sequence("HarassRetreat",
                    HasTarget(), IsInRange(15f),
                    SetFormation(FormationType.Skirmish), Retreat(15f)
                ),
                new Sequence("HarassFire",
                    HasTarget(), IsInRange(60f), HasAmmo(),
                    SetFormation(FormationType.Skirmish), FaceEnemy(), Attack()
                ),
                new Sequence("SkirmishAdvance",
                    HasTarget(), AdvanceToward(10f)
                ),
                FindNearestEnemy()
            );
        }

        public static BTNode GrenadierTactic()
        {
            return new Selector("GrenadierTactic",
                // COUNTER-FLANK (Legendary)
                new Sequence("GrenadierCounterFlank",
                    DifficultyAtLeast(AIDifficulty.Legendary),
                    EnemyIsFlankingUs(),
                    CounterFlank()
                ),
                // Charge when close
                new Sequence("GrenadierCharge",
                    HasTarget(), IsInRange(25f),
                    MoraleAbove(50f), StaminaAbove(0.5f),
                    Charge()
                ),
                // PURSUIT (Hard+)
                new Sequence("GrenadierPursuit",
                    DifficultyAtLeast(AIDifficulty.Hard),
                    PhaseIs(BattlePhase.Pursuit),
                    PursueRoutingEnemy()
                ),
                InfantryLineTactic()
            );
        }
    }
}
