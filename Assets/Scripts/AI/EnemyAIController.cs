using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Core;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.AI
{
    /// <summary>
    /// Enemy AI controller that integrates with BattleAIBrain for strategic coordination.
    /// Uses behavior trees per regiment with difficulty-gated tactical branches.
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        [Header("AI Settings")]
        [SerializeField] private AIDifficulty difficulty = AIDifficulty.Normal;

        private float decisionInterval = 3f;
        private float decisionTimer;

        // Strategic brain
        private BattleAIBrain brain;

        // Per-regiment behavior trees and contexts
        private Dictionary<Regiment, BTNode> regimentTrees = new Dictionary<Regiment, BTNode>();
        private Dictionary<Regiment, BTContext> regimentContexts = new Dictionary<Regiment, BTContext>();

        private void Start()
        {
            // Sync difficulty from DifficultySettings if available
            if (DifficultySettings.Instance != null)
            {
                difficulty = MapDifficulty(DifficultySettings.Instance.CurrentDifficulty);
                decisionInterval = DifficultySettings.Instance.AIDecisionInterval;
            }
            else
            {
                // Fallback interval
                switch (difficulty)
                {
                    case AIDifficulty.Recruit:   decisionInterval = 6f;   break;
                    case AIDifficulty.Easy:      decisionInterval = 4f;   break;
                    case AIDifficulty.Normal:    decisionInterval = 3f;   break;
                    case AIDifficulty.Hard:      decisionInterval = 1.5f; break;
                    case AIDifficulty.Legendary: decisionInterval = 0.8f; break;
                }
            }

            decisionTimer = decisionInterval + Random.Range(0f, 1f);

            // Create the strategic brain
            brain = new BattleAIBrain(difficulty);

            Debug.Log($"[EnemyAI] Initialized with difficulty={difficulty}, interval={decisionInterval}s");
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Battle) return;

            decisionTimer -= Time.deltaTime;
            if (decisionTimer <= 0f)
            {
                decisionTimer = decisionInterval + Random.Range(-0.3f, 0.3f);
                StartCoroutine(MakeDecisionsSpread());
            }
        }

        private System.Collections.IEnumerator MakeDecisionsSpread()
        {
            if (BattleManager.Instance == null) yield break;

            var enemyRegiments = BattleManager.Instance.EnemyRegiments;
            var playerRegiments = BattleManager.Instance.PlayerRegiments;
            if (enemyRegiments == null) yield break;

            // ── STRATEGIC BRAIN: think before individual decisions ──
            brain.Think(enemyRegiments, playerRegiments, decisionInterval);

            // Process regiments spread across frames
            int processedPerFrame = Mathf.Max(1, enemyRegiments.Count / 10);
            int processed = 0;

            foreach (var regiment in enemyRegiments)
            {
                if (regiment.CachedAliveCount <= 0) continue;
                if (regiment.UnitData == null) continue;

                // Get role from brain
                TacticalRole role = brain.GetRole(regiment);

                // Reserve units hold position until committed
                if (role == TacticalRole.Reserve && !brain.IsReserveCommitted)
                {
                    var reserveCtx = GetOrCreateContext(regiment);
                    reserveCtx.Role = role;
                    reserveCtx.Phase = brain.CurrentPhase;
                    AITacticNodes.MoveToReservePosition().Tick(reserveCtx);
                    continue;
                }

                // Build/get behavior tree (cached per regiment)
                BTNode tree = GetOrCreateTree(regiment);
                BTContext context = GetOrCreateContext(regiment);
                context.DeltaTime = Time.deltaTime;
                context.Role = role;
                context.Phase = brain.CurrentPhase;

                tree.Tick(context);

                processed++;
                if (processed >= processedPerFrame)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }

        // ================================================================
        // BEHAVIOR TREE MANAGEMENT
        // ================================================================

        private BTNode GetOrCreateTree(Regiment regiment)
        {
            if (regimentTrees.TryGetValue(regiment, out BTNode existing))
                return existing;

            BTNode tree = BuildTreeForUnit(regiment.UnitData.unitType);
            regimentTrees[regiment] = tree;
            return tree;
        }

        private BTContext GetOrCreateContext(Regiment regiment)
        {
            if (regimentContexts.TryGetValue(regiment, out BTContext existing))
            {
                existing.Regiment = regiment;
                existing.BattleManager = BattleManager.Instance;
                return existing;
            }

            var ctx = new BTContext
            {
                Regiment = regiment,
                BattleManager = BattleManager.Instance,
                Difficulty = difficulty
            };
            regimentContexts[regiment] = ctx;
            return ctx;
        }

        // ================================================================
        // TREE BUILDING — difficulty-aware
        // ================================================================

        private BTNode BuildTreeForUnit(UnitType unitType)
        {
            switch (unitType)
            {
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    return BuildCavalryTree();

                case UnitType.Artillery:
                    return BuildArtilleryTree();

                case UnitType.LightInfantry:
                    return BuildSkirmisherTree();

                case UnitType.Grenadier:
                    return BuildGrenadierTree();

                default:
                    return BuildInfantryTree();
            }
        }

        private BTNode BuildInfantryTree()
        {
            if (difficulty <= AIDifficulty.Recruit)
            {
                // Recruit: simple find → advance → attack
                return new Selector("InfantryRecruit",
                    new Sequence("RecruitAttack",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.HasTarget(),
                        AITacticNodes.IsInRange(50f),
                        AITacticNodes.Attack()
                    ),
                    new Sequence("RecruitAdvance",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.HasTarget(),
                        AITacticNodes.AdvanceToward(10f)
                    ),
                    new Sequence("RecruitFind",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.FindNearestEnemy()
                    )
                );
            }

            // Normal+ uses the full InfantryLineTactic with embedded difficulty gates
            return new Selector("InfantryRoot",
                new Sequence("AliveCheck",
                    AITacticNodes.HasAliveUnits(),
                    AITacticNodes.InfantryLineTactic()
                )
            );
        }

        private BTNode BuildGrenadierTree()
        {
            if (difficulty <= AIDifficulty.Recruit)
            {
                return BuildInfantryTree(); // Recruit grenadiers act like basic infantry
            }

            return new Selector("GrenadierRoot",
                new Sequence("GrenadierAlive",
                    AITacticNodes.HasAliveUnits(),
                    AITacticNodes.GrenadierTactic()
                )
            );
        }

        private BTNode BuildCavalryTree()
        {
            if (difficulty <= AIDifficulty.Recruit)
            {
                // Recruit cavalry just charges directly
                return new Selector("CavalryRecruit",
                    new Sequence("RecruitCharge",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.HasTarget(),
                        AITacticNodes.IsInRange(20f),
                        AITacticNodes.Charge()
                    ),
                    new Sequence("RecruitCavAdvance",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.HasTarget(),
                        AITacticNodes.AdvanceToward(20f)
                    ),
                    new Sequence("RecruitCavFind",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.FindNearestEnemy()
                    )
                );
            }

            return new Selector("CavalryRoot",
                new Sequence("CavalryAlive",
                    AITacticNodes.HasAliveUnits(),
                    AITacticNodes.CavalryTactic()
                )
            );
        }

        private BTNode BuildArtilleryTree()
        {
            if (difficulty <= AIDifficulty.Easy)
            {
                // Easy/Recruit artillery: simple shoot at nearest
                return new Selector("ArtillerySimple",
                    new Sequence("ArtFire",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.HasTarget(),
                        AITacticNodes.IsInRange(120f),
                        AITacticNodes.FaceEnemy(),
                        AITacticNodes.Attack()
                    ),
                    new Sequence("ArtFind",
                        AITacticNodes.HasAliveUnits(),
                        AITacticNodes.FindNearestEnemy()
                    )
                );
            }

            // Hard+: artillery uses brain's focus fire target
            return new Selector("ArtilleryRoot",
                new Sequence("ArtilleryAlive",
                    AITacticNodes.HasAliveUnits(),
                    AITacticNodes.ArtilleryTacticWithBrain(brain)
                )
            );
        }

        private BTNode BuildSkirmisherTree()
        {
            if (difficulty <= AIDifficulty.Recruit)
            {
                return BuildInfantryTree(); // Recruit skirmishers are basic infantry
            }

            return new Selector("SkirmisherRoot",
                new Sequence("SkirmisherAlive",
                    AITacticNodes.HasAliveUnits(),
                    AITacticNodes.SkirmisherTactic()
                )
            );
        }

        // ================================================================
        // UTILITY
        // ================================================================

        public void SetDifficulty(AIDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
            decisionInterval = newDifficulty switch
            {
                AIDifficulty.Recruit => 6f,
                AIDifficulty.Easy => 4f,
                AIDifficulty.Normal => 3f,
                AIDifficulty.Hard => 1.5f,
                AIDifficulty.Legendary => 0.8f,
                _ => 3f
            };

            // Update brain
            brain?.SetDifficulty(newDifficulty);

            // Update all existing contexts
            foreach (var ctx in regimentContexts.Values)
                ctx.Difficulty = newDifficulty;

            // Rebuild all trees (they contain difficulty gates)
            regimentTrees.Clear();

            Debug.Log($"[EnemyAI] Difficulty changed to {newDifficulty}");
        }

        /// <summary>Map DifficultyLevel (Core) to AIDifficulty (AI)</summary>
        private AIDifficulty MapDifficulty(DifficultyLevel level)
        {
            return level switch
            {
                DifficultyLevel.Recruit => AIDifficulty.Recruit,
                DifficultyLevel.Easy => AIDifficulty.Easy,
                DifficultyLevel.Normal => AIDifficulty.Normal,
                DifficultyLevel.Hard => AIDifficulty.Hard,
                DifficultyLevel.Legendary => AIDifficulty.Legendary,
                _ => AIDifficulty.Normal
            };
        }
    }
}
