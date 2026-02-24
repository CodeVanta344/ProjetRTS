using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Advanced supply system with supply lines, depots, foraging, and attrition.
    /// Armies must maintain a supply chain back to a friendly depot or capital.
    /// </summary>

    public enum SupplyStatus
    {
        WellSupplied,   // Full supply chain to depot
        Adequate,       // Partial supply, minor penalty
        Strained,       // Limited supply, moderate penalty
        Critical,       // Almost no supply, severe penalty
        CutOff          // No supply at all, heavy attrition
    }

    [System.Serializable]
    public class SupplyDepot
    {
        public string provinceId;
        public FactionType owner;
        public int capacity = 500;        // Max supply stored
        public int currentSupply = 300;   // Current supply available
        public int resupplyRate = 50;     // Supply generated per turn
        public int level = 1;             // Depot level (1-3)
        public bool isCapitalDepot;       // Capital depots have higher capacity

        public SupplyDepot(string provinceId, FactionType owner, bool isCapital = false)
        {
            this.provinceId = provinceId;
            this.owner = owner;
            isCapitalDepot = isCapital;
            if (isCapital)
            {
                capacity = 1000;
                currentSupply = 800;
                resupplyRate = 100;
                level = 3;
            }
        }

        public void Upgrade()
        {
            if (level >= 3) return;
            level++;
            capacity += 250;
            resupplyRate += 25;
        }

        public void ProcessTurn()
        {
            currentSupply = Mathf.Min(capacity, currentSupply + resupplyRate);
        }

        public int DrawSupply(int requested)
        {
            int available = Mathf.Min(requested, currentSupply);
            currentSupply -= available;
            return available;
        }
    }

    [System.Serializable]
    public class SupplyLine
    {
        public string fromDepot;     // Depot province ID
        public string toProvince;    // Destination province
        public List<string> path;    // Province IDs forming the route
        public float efficiency;     // 0-1, based on distance and terrain
        public bool isBlocked;       // Enemy cut the line
        public int length;           // Number of provinces in chain

        public SupplyLine(string from, string to, List<string> path)
        {
            fromDepot = from;
            toProvince = to;
            this.path = path;
            length = path.Count;
            efficiency = CalculateEfficiency();
        }

        private float CalculateEfficiency()
        {
            if (length == 0) return 1f;
            // Efficiency decreases with distance: 100% at 0, ~50% at 5, ~25% at 10
            float eff = 1f / (1f + length * 0.15f);
            return Mathf.Clamp01(eff);
        }
    }

    public static class SupplyLineSystem
    {
        // Supply depots per faction
        private static Dictionary<FactionType, List<SupplyDepot>> depots =
            new Dictionary<FactionType, List<SupplyDepot>>();

        // Cached supply status per army
        private static Dictionary<string, SupplyStatus> armySupplyStatus =
            new Dictionary<string, SupplyStatus>();

        // Supply lines per army
        private static Dictionary<string, SupplyLine> armySupplyLines =
            new Dictionary<string, SupplyLine>();

        // === INITIALIZATION ===

        public static void Initialize(FactionType faction, string capitalProvinceId)
        {
            if (!depots.ContainsKey(faction))
                depots[faction] = new List<SupplyDepot>();

            // Create capital depot
            var capitalDepot = new SupplyDepot(capitalProvinceId, faction, true);
            depots[faction].Add(capitalDepot);
        }

        public static void BuildDepot(FactionType faction, string provinceId)
        {
            if (!depots.ContainsKey(faction))
                depots[faction] = new List<SupplyDepot>();

            // Check if depot already exists
            if (depots[faction].Exists(d => d.provinceId == provinceId))
                return;

            depots[faction].Add(new SupplyDepot(provinceId, faction));
        }

        public static void UpgradeDepot(FactionType faction, string provinceId)
        {
            if (!depots.ContainsKey(faction)) return;
            var depot = depots[faction].Find(d => d.provinceId == provinceId);
            depot?.Upgrade();
        }

        public static List<SupplyDepot> GetDepots(FactionType faction)
        {
            return depots.ContainsKey(faction) ? depots[faction] : new List<SupplyDepot>();
        }

        // === SUPPLY STATUS CALCULATION ===

        public static SupplyStatus GetArmySupplyStatus(string armyId)
        {
            return armySupplyStatus.ContainsKey(armyId) ? armySupplyStatus[armyId] : SupplyStatus.Adequate;
        }

        /// <summary>Calculate supply status for all armies of a faction</summary>
        public static void CalculateSupplyForFaction(FactionType faction, CampaignManager cm)
        {
            if (cm == null || !cm.Factions.ContainsKey(faction)) return;
            var fd = cm.Factions[faction];

            foreach (string armyId in fd.armyIds)
            {
                if (!cm.Armies.ContainsKey(armyId)) continue;
                var army = cm.Armies[armyId];

                // Find best supply line to any depot
                SupplyLine bestLine = FindBestSupplyLine(faction, army.currentProvinceId, cm);

                if (bestLine != null)
                {
                    armySupplyLines[armyId] = bestLine;

                    // Calculate supply draw
                    int armySize = army.regiments.Sum(r => r.currentSize);
                    int supplyNeeded = armySize / 5; // 1 supply per 5 men

                    // Find the depot and draw supply
                    var depot = GetDepotAt(faction, bestLine.fromDepot);
                    int supplyGot = depot != null ? depot.DrawSupply(supplyNeeded) : 0;

                    float supplyRatio = supplyNeeded > 0 ? (float)supplyGot / supplyNeeded : 1f;
                    supplyRatio *= bestLine.efficiency;

                    // Determine status
                    SupplyStatus status;
                    if (supplyRatio >= 0.8f) status = SupplyStatus.WellSupplied;
                    else if (supplyRatio >= 0.6f) status = SupplyStatus.Adequate;
                    else if (supplyRatio >= 0.3f) status = SupplyStatus.Strained;
                    else if (supplyRatio > 0f) status = SupplyStatus.Critical;
                    else status = SupplyStatus.CutOff;

                    armySupplyStatus[armyId] = status;
                }
                else
                {
                    // No supply line found
                    armySupplyStatus[armyId] = SupplyStatus.CutOff;
                    armySupplyLines.Remove(armyId);
                }
            }
        }

        /// <summary>Find best supply line from any depot to target province via BFS</summary>
        private static SupplyLine FindBestSupplyLine(FactionType faction, string targetProvince, CampaignManager cm)
        {
            if (!depots.ContainsKey(faction)) return null;

            SupplyLine best = null;
            float bestEfficiency = -1f;

            foreach (var depot in depots[faction])
            {
                if (depot.currentSupply <= 0) continue;

                // BFS from depot to army
                var path = FindPath(depot.provinceId, targetProvince, faction, cm);
                if (path == null) continue;

                var line = new SupplyLine(depot.provinceId, targetProvince, path);

                // Check if any province in path is enemy-controlled
                bool blocked = false;
                foreach (string pid in path)
                {
                    if (!cm.Provinces.ContainsKey(pid)) continue;
                    var prov = cm.Provinces[pid];
                    if (prov.owner != faction)
                    {
                        // Enemy province blocks supply unless we occupy it
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                {
                    line.isBlocked = true;
                    line.efficiency *= 0.2f; // Heavily reduced but not zero (foraging)
                }

                if (line.efficiency > bestEfficiency)
                {
                    bestEfficiency = line.efficiency;
                    best = line;
                }
            }

            return best;
        }

        /// <summary>Simple BFS pathfinding through provinces</summary>
        private static List<string> FindPath(string from, string to, FactionType faction, CampaignManager cm)
        {
            if (from == to) return new List<string>();

            var visited = new HashSet<string>();
            var queue = new Queue<(string province, List<string> path)>();
            queue.Enqueue((from, new List<string>()));
            visited.Add(from);

            int maxDepth = 15; // Max supply chain length

            while (queue.Count > 0)
            {
                var (current, path) = queue.Dequeue();
                if (path.Count >= maxDepth) continue;

                if (!cm.Provinces.ContainsKey(current)) continue;
                var prov = cm.Provinces[current];

                foreach (string neighbor in prov.neighborIds)
                {
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    var newPath = new List<string>(path) { neighbor };

                    if (neighbor == to)
                        return newPath;

                    queue.Enqueue((neighbor, newPath));
                }
            }

            return null; // No path found
        }

        private static SupplyDepot GetDepotAt(FactionType faction, string provinceId)
        {
            if (!depots.ContainsKey(faction)) return null;
            return depots[faction].Find(d => d.provinceId == provinceId);
        }

        // === SUPPLY EFFECTS ===

        /// <summary>Get attrition modifier based on supply status</summary>
        public static float GetAttritionModifier(SupplyStatus status) => status switch
        {
            SupplyStatus.WellSupplied => 0f,
            SupplyStatus.Adequate => 0.01f,
            SupplyStatus.Strained => 0.03f,
            SupplyStatus.Critical => 0.08f,
            SupplyStatus.CutOff => 0.15f,
            _ => 0f
        };

        /// <summary>Get combat modifier based on supply status</summary>
        public static float GetCombatModifier(SupplyStatus status) => status switch
        {
            SupplyStatus.WellSupplied => 1.0f,
            SupplyStatus.Adequate => 0.95f,
            SupplyStatus.Strained => 0.80f,
            SupplyStatus.Critical => 0.60f,
            SupplyStatus.CutOff => 0.40f,
            _ => 1f
        };

        /// <summary>Get morale modifier based on supply status</summary>
        public static float GetMoraleModifier(SupplyStatus status) => status switch
        {
            SupplyStatus.WellSupplied => 0f,
            SupplyStatus.Adequate => -2f,
            SupplyStatus.Strained => -10f,
            SupplyStatus.Critical => -25f,
            SupplyStatus.CutOff => -40f,
            _ => 0f
        };

        /// <summary>Get movement modifier based on supply status</summary>
        public static float GetMovementModifier(SupplyStatus status) => status switch
        {
            SupplyStatus.WellSupplied => 0f,
            SupplyStatus.Adequate => 0f,
            SupplyStatus.Strained => -1f,
            SupplyStatus.Critical => -2f,
            SupplyStatus.CutOff => -3f,
            _ => 0f
        };

        // === TURN PROCESSING ===

        /// <summary>Process supply for all factions</summary>
        public static void ProcessSupplyTurn(CampaignManager cm)
        {
            // Resupply all depots
            foreach (var kvp in depots)
            {
                foreach (var depot in kvp.Value)
                    depot.ProcessTurn();
            }

            // Calculate supply for all factions
            foreach (var kvp in cm.Factions)
            {
                if (kvp.Value.isEliminated) continue;
                CalculateSupplyForFaction(kvp.Key, cm);
            }

            // Apply attrition from supply status
            foreach (var kvp in armySupplyStatus)
            {
                string armyId = kvp.Key;
                SupplyStatus status = kvp.Value;

                if (status == SupplyStatus.WellSupplied) continue;
                if (!cm.Armies.ContainsKey(armyId)) continue;
                var army = cm.Armies[armyId];

                float attrition = GetAttritionModifier(status);
                if (attrition <= 0f) continue;

                foreach (var reg in army.regiments)
                {
                    int losses = Mathf.RoundToInt(reg.currentSize * attrition);
                    reg.currentSize = Mathf.Max(1, reg.currentSize - losses);
                }

                if (status >= SupplyStatus.Strained)
                {
                    Debug.Log($"[Supply] {army.armyName} ({army.faction}) is {GetStatusName(status)} — losing {attrition * 100f:F0}% per turn");
                }
            }
        }

        /// <summary>Check if depot exists at province for faction</summary>
        public static bool HasDepot(FactionType faction, string provinceId)
        {
            if (!depots.ContainsKey(faction)) return false;
            return depots[faction].Exists(d => d.provinceId == provinceId);
        }

        /// <summary>Remove depots in provinces no longer owned by faction</summary>
        public static void CleanupDepots(CampaignManager cm)
        {
            foreach (var kvp in depots)
            {
                kvp.Value.RemoveAll(d =>
                {
                    if (!cm.Provinces.ContainsKey(d.provinceId)) return true;
                    return cm.Provinces[d.provinceId].owner != kvp.Key;
                });
            }
        }

        // === DISPLAY HELPERS ===

        public static string GetStatusName(SupplyStatus status) => status switch
        {
            SupplyStatus.WellSupplied => "Bien approvisionné",
            SupplyStatus.Adequate => "Approvisionné",
            SupplyStatus.Strained => "Sous tension",
            SupplyStatus.Critical => "Critique",
            SupplyStatus.CutOff => "Coupé!",
            _ => "?"
        };

        public static Color GetStatusColor(SupplyStatus status) => status switch
        {
            SupplyStatus.WellSupplied => Color.green,
            SupplyStatus.Adequate => new Color(0.7f, 1f, 0.3f),
            SupplyStatus.Strained => Color.yellow,
            SupplyStatus.Critical => new Color(1f, 0.5f, 0f),
            SupplyStatus.CutOff => Color.red,
            _ => Color.white
        };

        public static SupplyLine GetArmySupplyLine(string armyId)
        {
            return armySupplyLines.ContainsKey(armyId) ? armySupplyLines[armyId] : null;
        }

        /// <summary>Get total depot count for faction</summary>
        public static int GetDepotCount(FactionType faction)
        {
            return depots.ContainsKey(faction) ? depots[faction].Count : 0;
        }

        /// <summary>Get total supply stored across all depots</summary>
        public static int GetTotalSupply(FactionType faction)
        {
            if (!depots.ContainsKey(faction)) return 0;
            return depots[faction].Sum(d => d.currentSupply);
        }
    }
}
