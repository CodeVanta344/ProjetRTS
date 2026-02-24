using System.Collections.Generic;
using UnityEngine;

namespace NapoleonicWars.AI
{
    /// <summary>
    /// Lightweight Behavior Tree system for RTS AI.
    /// Supports: Selector, Sequence, Condition, Action, Decorator, Parallel nodes.
    /// </summary>

    public enum BTStatus
    {
        Success,
        Failure,
        Running
    }

    // ============================================================
    // BASE NODE
    // ============================================================

    public abstract class BTNode
    {
        public string Name { get; set; }
        public abstract BTStatus Tick(BTContext context);

        public BTNode(string name = "")
        {
            Name = name;
        }
    }

    // ============================================================
    // CONTEXT (shared blackboard for the tree)
    // ============================================================

    public class BTContext
    {
        public Units.Regiment Regiment { get; set; }
        public Units.Regiment TargetRegiment { get; set; }
        public Vector3 TargetPosition { get; set; }
        public Core.BattleManager BattleManager { get; set; }
        public float DeltaTime { get; set; }
        public AIDifficulty Difficulty { get; set; }
        public TacticalRole Role { get; set; } = TacticalRole.MainLine;
        public BattlePhase Phase { get; set; } = BattlePhase.Opening;

        // Blackboard for arbitrary data
        private Dictionary<string, object> blackboard = new Dictionary<string, object>();

        public void Set<T>(string key, T value) => blackboard[key] = value;

        public T Get<T>(string key, T defaultValue = default)
        {
            if (blackboard.TryGetValue(key, out object val) && val is T typed)
                return typed;
            return defaultValue;
        }

        public bool Has(string key) => blackboard.ContainsKey(key);
    }

    public enum AIDifficulty
    {
        Recruit,
        Easy,
        Normal,
        Hard,
        Legendary
    }

    public enum TacticalRole
    {
        MainLine,
        Reserve,
        FlankingForce,
        SkirmishScreen,
        ArtillerySupport
    }

    public enum BattlePhase
    {
        Opening,
        Engagement,
        Crisis,
        Pursuit,
        Retreat
    }

    // ============================================================
    // COMPOSITE NODES
    // ============================================================

    /// <summary>
    /// Tries children in order. Returns Success on first child success.
    /// Like an OR gate.
    /// </summary>
    public class Selector : BTNode
    {
        private List<BTNode> children = new List<BTNode>();

        public Selector(string name, params BTNode[] nodes) : base(name)
        {
            children.AddRange(nodes);
        }

        public override BTStatus Tick(BTContext context)
        {
            foreach (var child in children)
            {
                BTStatus status = child.Tick(context);
                if (status != BTStatus.Failure)
                    return status;
            }
            return BTStatus.Failure;
        }
    }

    /// <summary>
    /// Runs children in order. Returns Failure on first child failure.
    /// Like an AND gate.
    /// </summary>
    public class Sequence : BTNode
    {
        private List<BTNode> children = new List<BTNode>();

        public Sequence(string name, params BTNode[] nodes) : base(name)
        {
            children.AddRange(nodes);
        }

        public override BTStatus Tick(BTContext context)
        {
            foreach (var child in children)
            {
                BTStatus status = child.Tick(context);
                if (status != BTStatus.Success)
                    return status;
            }
            return BTStatus.Success;
        }
    }

    /// <summary>
    /// Runs all children simultaneously. Succeeds if required number succeed.
    /// </summary>
    public class Parallel : BTNode
    {
        private List<BTNode> children = new List<BTNode>();
        private int requiredSuccesses;

        public Parallel(string name, int requiredSuccesses, params BTNode[] nodes) : base(name)
        {
            this.requiredSuccesses = requiredSuccesses;
            children.AddRange(nodes);
        }

        public override BTStatus Tick(BTContext context)
        {
            int successes = 0;
            int failures = 0;

            foreach (var child in children)
            {
                BTStatus status = child.Tick(context);
                if (status == BTStatus.Success) successes++;
                if (status == BTStatus.Failure) failures++;
            }

            if (successes >= requiredSuccesses) return BTStatus.Success;
            if (failures > children.Count - requiredSuccesses) return BTStatus.Failure;
            return BTStatus.Running;
        }
    }

    // ============================================================
    // DECORATOR NODES
    // ============================================================

    /// <summary>
    /// Inverts the result of its child.
    /// </summary>
    public class Inverter : BTNode
    {
        private BTNode child;

        public Inverter(string name, BTNode child) : base(name)
        {
            this.child = child;
        }

        public override BTStatus Tick(BTContext context)
        {
            BTStatus status = child.Tick(context);
            if (status == BTStatus.Success) return BTStatus.Failure;
            if (status == BTStatus.Failure) return BTStatus.Success;
            return BTStatus.Running;
        }
    }

    /// <summary>
    /// Repeats child N times or until failure.
    /// </summary>
    public class Repeater : BTNode
    {
        private BTNode child;
        private int maxRepeats;

        public Repeater(string name, BTNode child, int maxRepeats = -1) : base(name)
        {
            this.child = child;
            this.maxRepeats = maxRepeats;
        }

        public override BTStatus Tick(BTContext context)
        {
            int count = maxRepeats <= 0 ? 1 : maxRepeats;
            for (int i = 0; i < count; i++)
            {
                BTStatus status = child.Tick(context);
                if (status == BTStatus.Failure) return BTStatus.Failure;
                if (status == BTStatus.Running) return BTStatus.Running;
            }
            return BTStatus.Success;
        }
    }

    /// <summary>
    /// Always returns a specific status regardless of child result.
    /// </summary>
    public class AlwaysReturn : BTNode
    {
        private BTNode child;
        private BTStatus returnStatus;

        public AlwaysReturn(string name, BTNode child, BTStatus returnStatus) : base(name)
        {
            this.child = child;
            this.returnStatus = returnStatus;
        }

        public override BTStatus Tick(BTContext context)
        {
            child.Tick(context);
            return returnStatus;
        }
    }

    // ============================================================
    // LEAF NODES
    // ============================================================

    /// <summary>
    /// Evaluates a condition. Returns Success if true, Failure if false.
    /// </summary>
    public class Condition : BTNode
    {
        private System.Func<BTContext, bool> predicate;

        public Condition(string name, System.Func<BTContext, bool> predicate) : base(name)
        {
            this.predicate = predicate;
        }

        public override BTStatus Tick(BTContext context)
        {
            return predicate(context) ? BTStatus.Success : BTStatus.Failure;
        }
    }

    /// <summary>
    /// Executes an action. Returns the action's result status.
    /// </summary>
    public class BTAction : BTNode
    {
        private System.Func<BTContext, BTStatus> action;

        public BTAction(string name, System.Func<BTContext, BTStatus> action) : base(name)
        {
            this.action = action;
        }

        public override BTStatus Tick(BTContext context)
        {
            return action(context);
        }
    }

    /// <summary>
    /// Random selector — shuffles children before selecting (adds unpredictability).
    /// </summary>
    public class RandomSelector : BTNode
    {
        private List<BTNode> children = new List<BTNode>();

        public RandomSelector(string name, params BTNode[] nodes) : base(name)
        {
            children.AddRange(nodes);
        }

        public override BTStatus Tick(BTContext context)
        {
            // Fisher-Yates shuffle
            for (int i = children.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = children[i];
                children[i] = children[j];
                children[j] = temp;
            }

            foreach (var child in children)
            {
                BTStatus status = child.Tick(context);
                if (status != BTStatus.Failure)
                    return status;
            }
            return BTStatus.Failure;
        }
    }
}
