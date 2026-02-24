using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    // === CASUS BELLI ===

    public enum CasusBelliType
    {
        None,
        Conquest,           // Take provinces by force (high war support cost)
        Reconquest,         // Retake core provinces (low cost)
        Liberation,         // Free a nation from occupation
        Subjugation,        // Force vassalization
        Humiliation,        // Reduce enemy prestige/PP
        DefensiveWar,       // Automatic when attacked
        ClaimedTerritory,   // Province claimed via focus tree
        ReligiousWar,       // Attack different religion (requires Theocracy law)
        RevolutionaryWar,   // Spread revolution (France-specific)
        Succession,         // Dynastic claim
        PunitiveExpedition, // Punish for treaty violation
        Colonial             // Colonial expansion
    }

    [System.Serializable]
    public class CasusBelli
    {
        public CasusBelliType type;
        public FactionType attacker;
        public FactionType defender;
        public string targetProvince;   // Optional: specific target
        public float warSupportCost;    // War support needed to declare
        public float ppCost;            // PP cost to justify
        public int justificationTurns;  // Turns to fabricate CB
        public int turnsRemaining;      // Turns left in fabrication
        public bool isJustified;        // Ready to use

        public CasusBelli(CasusBelliType type, FactionType attacker, FactionType defender)
        {
            this.type = type;
            this.attacker = attacker;
            this.defender = defender;
            var info = WarJustificationSystem.GetCBInfo(type);
            warSupportCost = info.warSupportCost;
            ppCost = info.ppCost;
            justificationTurns = info.justificationTurns;
            turnsRemaining = justificationTurns;
            isJustified = info.autoJustified;
        }
    }

    public struct CBInfo
    {
        public float warSupportCost;
        public float ppCost;
        public int justificationTurns;
        public bool autoJustified;
        public float aggressiveExpansion; // How much AE this generates
    }

    // === COALITIONS ===

    [System.Serializable]
    public class Coalition
    {
        public string coalitionId;
        public FactionType leader;
        public FactionType targetEnemy;
        public List<FactionType> members = new List<FactionType>();
        public float totalStrength;
        public int formedOnTurn;

        public Coalition(string id, FactionType leader, FactionType enemy, int turn)
        {
            coalitionId = id;
            this.leader = leader;
            targetEnemy = enemy;
            members.Add(leader);
            formedOnTurn = turn;
        }

        public void AddMember(FactionType faction)
        {
            if (!members.Contains(faction))
                members.Add(faction);
        }

        public bool IsMember(FactionType faction) => members.Contains(faction);
    }

    // === VICTORY CONDITIONS ===

    public enum VictoryType
    {
        DominationVictory,   // Control X% of provinces
        ConquestVictory,     // Eliminate all rivals
        PrestigeVictory,     // Reach prestige threshold
        EconomicVictory,     // Reach economic threshold
        SurvivalVictory,     // Survive X turns as minor nation
        RevolutionaryVictory // Spread revolution to X nations (France)
    }

    [System.Serializable]
    public class VictoryCondition
    {
        public VictoryType type;
        public string description;
        public float progress;      // 0-1
        public float threshold;     // Target value
        public bool isAchieved;

        public VictoryCondition(VictoryType type, string desc, float threshold)
        {
            this.type = type;
            description = desc;
            this.threshold = threshold;
        }
    }

    // === MAIN SYSTEM ===

    public static class WarJustificationSystem
    {
        // Active justifications being fabricated
        private static Dictionary<FactionType, List<CasusBelli>> activeJustifications =
            new Dictionary<FactionType, List<CasusBelli>>();

        // Justified CBs ready to use
        private static Dictionary<FactionType, List<CasusBelli>> justifiedCBs =
            new Dictionary<FactionType, List<CasusBelli>>();

        // Active coalitions
        private static List<Coalition> activeCoalitions = new List<Coalition>();

        // Aggressive expansion per faction
        private static Dictionary<FactionType, float> aggressiveExpansion =
            new Dictionary<FactionType, float>();

        // Victory conditions per faction
        private static Dictionary<FactionType, List<VictoryCondition>> victoryConditions =
            new Dictionary<FactionType, List<VictoryCondition>>();

        // War score tracking
        private static Dictionary<string, float> warScores = new Dictionary<string, float>();

        public static float GetAggressiveExpansion(FactionType f)
        {
            return aggressiveExpansion.ContainsKey(f) ? aggressiveExpansion[f] : 0f;
        }

        public static List<Coalition> ActiveCoalitions => activeCoalitions;

        // === CB INFO ===

        public static CBInfo GetCBInfo(CasusBelliType type) => type switch
        {
            CasusBelliType.Conquest => new CBInfo
                { warSupportCost = 0.30f, ppCost = 50f, justificationTurns = 8, aggressiveExpansion = 15f },
            CasusBelliType.Reconquest => new CBInfo
                { warSupportCost = 0.10f, ppCost = 20f, justificationTurns = 0, autoJustified = true, aggressiveExpansion = 2f },
            CasusBelliType.Liberation => new CBInfo
                { warSupportCost = 0.15f, ppCost = 30f, justificationTurns = 4, aggressiveExpansion = 0f },
            CasusBelliType.Subjugation => new CBInfo
                { warSupportCost = 0.35f, ppCost = 75f, justificationTurns = 10, aggressiveExpansion = 20f },
            CasusBelliType.Humiliation => new CBInfo
                { warSupportCost = 0.20f, ppCost = 40f, justificationTurns = 6, aggressiveExpansion = 5f },
            CasusBelliType.DefensiveWar => new CBInfo
                { warSupportCost = 0f, ppCost = 0f, justificationTurns = 0, autoJustified = true, aggressiveExpansion = 0f },
            CasusBelliType.ClaimedTerritory => new CBInfo
                { warSupportCost = 0.15f, ppCost = 25f, justificationTurns = 0, autoJustified = true, aggressiveExpansion = 5f },
            CasusBelliType.ReligiousWar => new CBInfo
                { warSupportCost = 0.25f, ppCost = 60f, justificationTurns = 6, aggressiveExpansion = 10f },
            CasusBelliType.RevolutionaryWar => new CBInfo
                { warSupportCost = 0.20f, ppCost = 35f, justificationTurns = 4, aggressiveExpansion = 8f },
            CasusBelliType.Succession => new CBInfo
                { warSupportCost = 0.20f, ppCost = 50f, justificationTurns = 6, aggressiveExpansion = 8f },
            CasusBelliType.PunitiveExpedition => new CBInfo
                { warSupportCost = 0.10f, ppCost = 30f, justificationTurns = 3, aggressiveExpansion = 3f },
            CasusBelliType.Colonial => new CBInfo
                { warSupportCost = 0.15f, ppCost = 40f, justificationTurns = 5, aggressiveExpansion = 3f },
            _ => new CBInfo { warSupportCost = 0.30f, ppCost = 50f, justificationTurns = 8, aggressiveExpansion = 10f }
        };

        // === JUSTIFICATION ===

        public static bool StartJustification(FactionType attacker, FactionType defender, CasusBelliType type)
        {
            if (!activeJustifications.ContainsKey(attacker))
                activeJustifications[attacker] = new List<CasusBelli>();

            // Check if already justifying against this target
            if (activeJustifications[attacker].Exists(cb => cb.defender == defender && cb.type == type))
                return false;

            var cb = new CasusBelli(type, attacker, defender);

            // Spend PP
            var cm = CampaignManager.Instance;
            if (cm != null && cm.Factions.ContainsKey(attacker))
            {
                if (!cm.Factions[attacker].SpendPoliticalPower(cb.ppCost))
                    return false;
            }

            if (cb.isJustified)
            {
                // Auto-justified CBs go straight to ready
                if (!justifiedCBs.ContainsKey(attacker))
                    justifiedCBs[attacker] = new List<CasusBelli>();
                justifiedCBs[attacker].Add(cb);
            }
            else
            {
                activeJustifications[attacker].Add(cb);
            }

            Debug.Log($"[WarJustification] {attacker} started justifying {type} against {defender}");
            return true;
        }

        public static List<CasusBelli> GetJustifiedCBs(FactionType faction)
        {
            return justifiedCBs.ContainsKey(faction) ? justifiedCBs[faction] : new List<CasusBelli>();
        }

        public static List<CasusBelli> GetActiveJustifications(FactionType faction)
        {
            return activeJustifications.ContainsKey(faction) ? activeJustifications[faction] : new List<CasusBelli>();
        }

        /// <summary>Check if faction has a valid CB against another</summary>
        public static bool HasCasusBelli(FactionType attacker, FactionType defender)
        {
            if (!justifiedCBs.ContainsKey(attacker)) return false;
            return justifiedCBs[attacker].Exists(cb => cb.defender == defender);
        }

        /// <summary>Use a CB to declare war (removes it from the list)</summary>
        public static CasusBelli UseCasusBelli(FactionType attacker, FactionType defender)
        {
            if (!justifiedCBs.ContainsKey(attacker)) return null;
            var cb = justifiedCBs[attacker].Find(c => c.defender == defender);
            if (cb != null)
            {
                justifiedCBs[attacker].Remove(cb);

                // Apply aggressive expansion
                if (!aggressiveExpansion.ContainsKey(attacker))
                    aggressiveExpansion[attacker] = 0f;
                aggressiveExpansion[attacker] += GetCBInfo(cb.type).aggressiveExpansion;
            }
            return cb;
        }

        // === COALITIONS ===

        /// <summary>Form a coalition against an aggressor (high AE triggers this)</summary>
        public static Coalition FormCoalition(FactionType leader, FactionType enemy, int currentTurn)
        {
            // Check if coalition already exists against this enemy
            var existing = activeCoalitions.Find(c => c.targetEnemy == enemy);
            if (existing != null)
            {
                existing.AddMember(leader);
                return existing;
            }

            var coalition = new Coalition($"coalition_{enemy}_{currentTurn}", leader, enemy, currentTurn);
            activeCoalitions.Add(coalition);
            Debug.Log($"[Coalition] {leader} formed coalition against {enemy}!");
            return coalition;
        }

        public static void JoinCoalition(FactionType faction, FactionType enemy)
        {
            var coalition = activeCoalitions.Find(c => c.targetEnemy == enemy);
            if (coalition != null && !coalition.IsMember(faction))
            {
                coalition.AddMember(faction);
                Debug.Log($"[Coalition] {faction} joined coalition against {enemy}");
            }
        }

        public static Coalition GetCoalitionAgainst(FactionType faction)
        {
            return activeCoalitions.Find(c => c.targetEnemy == faction);
        }

        /// <summary>Check if any faction should form a coalition based on AE</summary>
        public static void CheckCoalitionFormation(int currentTurn)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            foreach (var kvp in aggressiveExpansion)
            {
                FactionType aggressor = kvp.Key;
                float ae = kvp.Value;

                // AE > 30 triggers coalition formation
                if (ae < 30f) continue;

                // Check if coalition already exists
                if (activeCoalitions.Exists(c => c.targetEnemy == aggressor)) continue;

                // Find potential coalition leader (strongest non-allied faction)
                FactionType bestLeader = FactionType.France; // default
                float bestStrength = 0;
                foreach (var fkvp in cm.Factions)
                {
                    if (fkvp.Key == aggressor || fkvp.Value.isEliminated) continue;
                    float strength = fkvp.Value.ownedProvinceIds.Count * 100f + fkvp.Value.manpower;
                    if (strength > bestStrength)
                    {
                        bestStrength = strength;
                        bestLeader = fkvp.Key;
                    }
                }

                var coalition = FormCoalition(bestLeader, aggressor, currentTurn);

                // Auto-join: factions with negative opinion of aggressor
                foreach (var fkvp in cm.Factions)
                {
                    if (fkvp.Key == aggressor || fkvp.Key == bestLeader || fkvp.Value.isEliminated) continue;
                    // Minor factions near the aggressor are scared
                    if (fkvp.Value.ownedProvinceIds.Count < 5 || ae > 50f)
                        coalition.AddMember(fkvp.Key);
                }
            }
        }

        // === VICTORY CONDITIONS ===

        public static void InitializeVictoryConditions(FactionType faction)
        {
            var conditions = new List<VictoryCondition>();

            var cm = CampaignManager.Instance;
            int totalProvinces = cm != null ? cm.Provinces.Count : 100;
            bool isMajor = IsMajorPower(faction);

            if (isMajor)
            {
                conditions.Add(new VictoryCondition(VictoryType.DominationVictory,
                    $"Contrôler {(int)(totalProvinces * 0.4f)} provinces ({40}%)", totalProvinces * 0.4f));
                conditions.Add(new VictoryCondition(VictoryType.PrestigeVictory,
                    "Accumuler 5000 points de prestige", 5000f));
                conditions.Add(new VictoryCondition(VictoryType.EconomicVictory,
                    "Accumuler 20000 or dans le trésor", 20000f));

                if (faction == FactionType.France)
                    conditions.Add(new VictoryCondition(VictoryType.RevolutionaryVictory,
                        "Répandre la Révolution dans 8 nations", 8f));
            }
            else
            {
                conditions.Add(new VictoryCondition(VictoryType.SurvivalVictory,
                    "Survivre 60 tours en tant que nation indépendante", 60f));
                conditions.Add(new VictoryCondition(VictoryType.EconomicVictory,
                    "Accumuler 10000 or dans le trésor", 10000f));
                conditions.Add(new VictoryCondition(VictoryType.DominationVictory,
                    $"Contrôler {(int)(totalProvinces * 0.15f)} provinces", totalProvinces * 0.15f));
            }

            victoryConditions[faction] = conditions;
        }

        public static List<VictoryCondition> GetVictoryConditions(FactionType faction)
        {
            if (!victoryConditions.ContainsKey(faction))
                InitializeVictoryConditions(faction);
            return victoryConditions[faction];
        }

        /// <summary>Update victory progress at end of turn</summary>
        public static VictoryCondition CheckVictoryProgress(FactionType faction, int currentTurn)
        {
            var cm = CampaignManager.Instance;
            if (cm == null || !cm.Factions.ContainsKey(faction)) return null;
            var fd = cm.Factions[faction];

            if (!victoryConditions.ContainsKey(faction))
                InitializeVictoryConditions(faction);

            foreach (var vc in victoryConditions[faction])
            {
                if (vc.isAchieved) continue;

                switch (vc.type)
                {
                    case VictoryType.DominationVictory:
                        vc.progress = fd.ownedProvinceIds.Count;
                        break;
                    case VictoryType.PrestigeVictory:
                        vc.progress = fd.politicalPower * 10f + fd.ownedProvinceIds.Count * 50f;
                        break;
                    case VictoryType.EconomicVictory:
                        vc.progress = fd.gold;
                        break;
                    case VictoryType.SurvivalVictory:
                        vc.progress = currentTurn;
                        break;
                    case VictoryType.RevolutionaryVictory:
                        // Count nations where France has influence
                        vc.progress = NationalFocusTree.GetCompletedCount(faction);
                        break;
                }

                if (vc.progress >= vc.threshold)
                {
                    vc.isAchieved = true;
                    Debug.Log($"[Victory] {faction} achieved {vc.type}! {vc.description}");
                    return vc;
                }
            }
            return null;
        }

        // === TURN PROCESSING ===

        public static void ProcessTurn(int currentTurn)
        {
            // Advance justifications
            foreach (var kvp in activeJustifications)
            {
                FactionType faction = kvp.Key;
                var toRemove = new List<CasusBelli>();

                foreach (var cb in kvp.Value)
                {
                    cb.turnsRemaining--;
                    if (cb.turnsRemaining <= 0)
                    {
                        cb.isJustified = true;
                        if (!justifiedCBs.ContainsKey(faction))
                            justifiedCBs[faction] = new List<CasusBelli>();
                        justifiedCBs[faction].Add(cb);
                        toRemove.Add(cb);

                        Debug.Log($"[WarJustification] {faction} justified {cb.type} against {cb.defender}!");
                        CampaignManager.Instance?.AddFactionEvent(faction,
                            $"Casus Belli justifié: {GetCBName(cb.type)} contre {cb.defender}");
                    }
                }
                foreach (var cb in toRemove)
                    kvp.Value.Remove(cb);
            }

            // Decay aggressive expansion (−1 per turn)
            var aeKeys = new List<FactionType>(aggressiveExpansion.Keys);
            foreach (var key in aeKeys)
            {
                aggressiveExpansion[key] = Mathf.Max(0f, aggressiveExpansion[key] - 1f);
            }

            // Check coalition formation
            CheckCoalitionFormation(currentTurn);

            // Dissolve coalitions if AE drops below threshold
            activeCoalitions.RemoveAll(c =>
            {
                if (!aggressiveExpansion.ContainsKey(c.targetEnemy)) return true;
                return aggressiveExpansion[c.targetEnemy] < 10f;
            });
        }

        // === WAR SCORE ===

        public static string GetWarKey(FactionType a, FactionType b)
        {
            return a < b ? $"{a}_vs_{b}" : $"{b}_vs_{a}";
        }

        public static void AddWarScore(FactionType winner, FactionType loser, float amount)
        {
            string key = GetWarKey(winner, loser);
            if (!warScores.ContainsKey(key)) warScores[key] = 0f;
            // Positive = winner advantage
            if (winner < loser) warScores[key] += amount;
            else warScores[key] -= amount;
        }

        public static float GetWarScore(FactionType perspective, FactionType opponent)
        {
            string key = GetWarKey(perspective, opponent);
            if (!warScores.ContainsKey(key)) return 0f;
            float score = warScores[key];
            return perspective < opponent ? score : -score;
        }

        /// <summary>Can force peace at 100% war score</summary>
        public static bool CanForcePeace(FactionType winner, FactionType loser)
        {
            return GetWarScore(winner, loser) >= 100f;
        }

        // === HELPERS ===

        public static bool IsMajorPower(FactionType f) => f switch
        {
            FactionType.France or FactionType.Britain or FactionType.Prussia or
            FactionType.Russia or FactionType.Austria or FactionType.Spain or
            FactionType.Ottoman => true,
            _ => false
        };

        public static string GetCBName(CasusBelliType type) => type switch
        {
            CasusBelliType.Conquest => "Conquête",
            CasusBelliType.Reconquest => "Reconquête",
            CasusBelliType.Liberation => "Libération",
            CasusBelliType.Subjugation => "Assujettissement",
            CasusBelliType.Humiliation => "Humiliation",
            CasusBelliType.DefensiveWar => "Guerre défensive",
            CasusBelliType.ClaimedTerritory => "Territoire revendiqué",
            CasusBelliType.ReligiousWar => "Guerre de religion",
            CasusBelliType.RevolutionaryWar => "Guerre révolutionnaire",
            CasusBelliType.Succession => "Succession",
            CasusBelliType.PunitiveExpedition => "Expédition punitive",
            CasusBelliType.Colonial => "Conquête coloniale",
            _ => "Inconnu"
        };

        public static string GetCBDescription(CasusBelliType type) => type switch
        {
            CasusBelliType.Conquest => "Prendre des provinces par la force",
            CasusBelliType.Reconquest => "Reprendre des provinces de cœur",
            CasusBelliType.Liberation => "Libérer une nation de l'occupation",
            CasusBelliType.Subjugation => "Forcer la vassalisation",
            CasusBelliType.Humiliation => "Réduire le prestige ennemi",
            CasusBelliType.DefensiveWar => "Repousser l'envahisseur",
            CasusBelliType.ClaimedTerritory => "Conquérir un territoire revendiqué",
            CasusBelliType.ReligiousWar => "Guerre sainte contre les infidèles",
            CasusBelliType.RevolutionaryWar => "Répandre la Révolution",
            CasusBelliType.Succession => "Revendiquer le trône",
            CasusBelliType.PunitiveExpedition => "Punir une violation de traité",
            CasusBelliType.Colonial => "Expansion coloniale",
            _ => ""
        };

        public static string GetVictoryName(VictoryType type) => type switch
        {
            VictoryType.DominationVictory => "Domination",
            VictoryType.ConquestVictory => "Conquête totale",
            VictoryType.PrestigeVictory => "Prestige",
            VictoryType.EconomicVictory => "Victoire économique",
            VictoryType.SurvivalVictory => "Survie",
            VictoryType.RevolutionaryVictory => "Révolution mondiale",
            _ => "?"
        };
    }
}
