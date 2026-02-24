using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Strategic AI for campaign-level decisions: diplomacy, recruitment, production,
    /// army movement, research, focus selection, law changes, and war declarations.
    /// </summary>
    public static class AICampaignManager
    {
        // AI personality traits per faction
        private static Dictionary<FactionType, AIPersonality> personalities =
            new Dictionary<FactionType, AIPersonality>();

        public struct AIPersonality
        {
            public float aggressiveness;  // 0-1: tendency to declare war
            public float expansionism;    // 0-1: desire to conquer
            public float defensiveness;   // 0-1: priority on defense
            public float diplomacy;       // 0-1: preference for diplomacy
            public float economy;         // 0-1: focus on economic development
            public float naval;           // 0-1: naval priority
        }

        static AICampaignManager()
        {
            InitializePersonalities();
        }

        private static void InitializePersonalities()
        {
            personalities[FactionType.France] = new AIPersonality
                { aggressiveness = 0.9f, expansionism = 0.9f, defensiveness = 0.3f, diplomacy = 0.5f, economy = 0.6f, naval = 0.3f };
            personalities[FactionType.Britain] = new AIPersonality
                { aggressiveness = 0.5f, expansionism = 0.4f, defensiveness = 0.6f, diplomacy = 0.8f, economy = 0.9f, naval = 0.95f };
            personalities[FactionType.Prussia] = new AIPersonality
                { aggressiveness = 0.7f, expansionism = 0.6f, defensiveness = 0.7f, diplomacy = 0.5f, economy = 0.6f, naval = 0.1f };
            personalities[FactionType.Russia] = new AIPersonality
                { aggressiveness = 0.6f, expansionism = 0.7f, defensiveness = 0.8f, diplomacy = 0.4f, economy = 0.5f, naval = 0.2f };
            personalities[FactionType.Austria] = new AIPersonality
                { aggressiveness = 0.5f, expansionism = 0.5f, defensiveness = 0.7f, diplomacy = 0.8f, economy = 0.7f, naval = 0.1f };
            personalities[FactionType.Spain] = new AIPersonality
                { aggressiveness = 0.4f, expansionism = 0.3f, defensiveness = 0.8f, diplomacy = 0.5f, economy = 0.5f, naval = 0.4f };
            personalities[FactionType.Ottoman] = new AIPersonality
                { aggressiveness = 0.6f, expansionism = 0.6f, defensiveness = 0.6f, diplomacy = 0.4f, economy = 0.5f, naval = 0.3f };
        }

        private static AIPersonality GetPersonality(FactionType f)
        {
            return personalities.ContainsKey(f) ? personalities[f] :
                new AIPersonality { aggressiveness = 0.3f, expansionism = 0.3f, defensiveness = 0.5f, diplomacy = 0.5f, economy = 0.5f, naval = 0.2f };
        }

        /// <summary>Process all AI decisions for a faction during its turn</summary>
        public static void ProcessAITurn(FactionType faction, CampaignManager cm)
        {
            if (cm == null || !cm.Factions.ContainsKey(faction)) return;
            var fd = cm.Factions[faction];
            if (fd.isEliminated) return;

            var personality = GetPersonality(faction);

            // 1. Focus tree
            AI_SelectFocus(faction, fd, personality);

            // 2. Research
            AI_SelectResearch(faction, fd, personality);

            // 3. Doctrine
            AI_SelectDoctrine(faction, personality);

            // 4. Recruitment
            AI_Recruit(faction, fd, cm, personality);

            // 5. Building
            AI_Build(faction, fd, cm, personality);

            // 6. Army movement
            AI_MoveArmies(faction, fd, cm, personality);

            // 7. Diplomacy
            AI_Diplomacy(faction, fd, cm, personality);

            // 8. Budget
            AI_AdjustBudget(faction, personality);

            // 9. Laws (occasionally)
            if (Random.value < 0.1f) AI_ChangeLaws(faction, fd, personality);
        }

        // === FOCUS TREE ===
        private static void AI_SelectFocus(FactionType faction, FactionData fd, AIPersonality p)
        {
            if (NationalFocusTree.GetActiveFocus(faction) != null) return;

            var tree = NationalFocusTree.GetFocusTree(faction);
            var available = new List<NationalFocus>();
            foreach (var f in tree)
            {
                if (f.isCompleted) continue;
                bool prereqsMet = true;
                foreach (string prereq in f.prerequisites)
                {
                    var pf = tree.Find(x => x.focusId == prereq);
                    if (pf == null || !pf.isCompleted) { prereqsMet = false; break; }
                }
                if (!prereqsMet) continue;
                if (f.mutuallyExclusive != null)
                {
                    bool blocked = false;
                    foreach (string excl in f.mutuallyExclusive)
                    {
                        var ef = tree.Find(x => x.focusId == excl);
                        if (ef != null && ef.isCompleted) { blocked = true; break; }
                    }
                    if (blocked) continue;
                }
                available.Add(f);
            }

            if (available.Count == 0) return;

            // Score each focus based on personality
            NationalFocus best = available[0];
            float bestScore = -999f;
            foreach (var f in available)
            {
                float score = 0;
                score += f.militaryFactoryReward * 10f * p.aggressiveness;
                score += f.manpowerReward * 0.001f * p.aggressiveness;
                score += f.civilianFactoryReward * 10f * p.economy;
                score += f.goldReward * 0.01f * p.economy;
                score += f.stabilityChange * 100f * p.defensiveness;
                score += f.warSupportChange * 80f * p.aggressiveness;
                score += f.navalYardReward * 15f * p.naval;
                score += f.ppReward * 0.5f * p.diplomacy;
                score += f.researchSpeedBonus * 200f * p.economy;
                score -= f.turnsToComplete * 2f; // Prefer shorter focuses
                if (score > bestScore) { bestScore = score; best = f; }
            }

            NationalFocusTree.StartFocus(faction, best.focusId);
        }

        // === RESEARCH ===
        private static void AI_SelectResearch(FactionType faction, FactionData fd, AIPersonality p)
        {
            if (fd.techTree == null || fd.techTree.IsResearching()) return;

            var available = fd.techTree.GetAvailableResearch();
            if (available == null || available.Count == 0) return;

            // Score techs
            Technology best = available[0];
            float bestScore = -999f;
            foreach (var tech in available)
            {
                float score = 0;
                score += (tech.category == TechCategory.Military ? 30f : 0f) * p.aggressiveness;
                score += (tech.category == TechCategory.Economy ? 30f : 0f) * p.economy;
                score += (tech.category == TechCategory.Diplomacy ? 20f : 0f) * p.diplomacy;
                score -= tech.turnsToResearch * 0.5f;
                if (score > bestScore) { bestScore = score; best = tech; }
            }

            fd.techTree.StartResearch(best.id, fd);
        }

        // === DOCTRINE ===
        private static void AI_SelectDoctrine(FactionType faction, AIPersonality p)
        {
            if (DoctrineTree.GetChosenDoctrine(faction) != DoctrineType.None) return;

            // Choose based on personality
            DoctrineType choice;
            if (p.naval > 0.7f) choice = DoctrineType.DoctrineNavale;
            else if (p.aggressiveness > 0.7f && p.expansionism > 0.6f) choice = DoctrineType.GrandeBatterie;
            else if (p.aggressiveness > 0.5f) choice = DoctrineType.GuerreDeMouvement;
            else if (p.defensiveness > 0.7f) choice = DoctrineType.DoctrineDefensive;
            else choice = DoctrineType.DoctrineDeLaMasse;

            DoctrineTree.ChooseDoctrine(faction, choice);
        }

        // === RECRUITMENT ===
        private static void AI_Recruit(FactionType faction, FactionData fd, CampaignManager cm, AIPersonality p)
        {
            // Only recruit if we have gold and manpower
            if (fd.gold < 200f || fd.manpower < 1000) return;

            // Find armies that need reinforcement
            foreach (string armyId in fd.armyIds)
            {
                if (!cm.Armies.ContainsKey(armyId)) continue;
                var army = cm.Armies[armyId];

                // Reinforce understrength regiments
                foreach (var reg in army.regiments)
                {
                    if (reg.currentSize < reg.maxSize * 0.7f && fd.manpower > 500)
                    {
                        int reinforce = Mathf.Min(reg.maxSize - reg.currentSize, fd.manpower / 2);
                        reg.currentSize += reinforce;
                        fd.manpower -= reinforce;
                    }
                }

                // Add new regiment if army is small and we have resources
                if (army.regiments.Count < 4 && fd.gold > 500f && fd.manpower > 2000)
                {
                    UnitType type = p.aggressiveness > 0.6f ? UnitType.LineInfantry : UnitType.LightInfantry;
                    if (army.regiments.Count(r => r.unitType == UnitType.Cavalry || r.unitType == UnitType.Lancer) == 0
                        && Random.value < 0.3f)
                        type = UnitType.Cavalry;

                    army.AddRegiment(new RegimentData($"AI_{faction}_{Random.Range(100, 999)}", type, 60));
                    fd.gold -= 200f;
                    fd.manpower -= 1500;
                }
            }

            // Create new army if we have lots of resources but few armies
            if (fd.armyIds.Count < 2 && fd.gold > 800f && fd.manpower > 5000 && fd.ownedProvinceIds.Count > 0)
            {
                string provId = fd.ownedProvinceIds[Random.Range(0, fd.ownedProvinceIds.Count)];
                string armyId = $"army_{faction}_{Random.Range(1000, 9999)}";
                var newArmy = new ArmyData(armyId, $"Armée de {faction}", faction, provId);
                newArmy.AddRegiment(new RegimentData($"{faction}_Inf1", UnitType.LineInfantry, 60));
                newArmy.AddRegiment(new RegimentData($"{faction}_Inf2", UnitType.LineInfantry, 60));
                if (p.aggressiveness > 0.5f)
                    newArmy.AddRegiment(new RegimentData($"{faction}_Cav", UnitType.Cavalry, 30));

                cm.Armies[armyId] = newArmy;
                fd.armyIds.Add(armyId);
                fd.gold -= 500f;
                fd.manpower -= 3000;
            }
        }

        // === BUILDING ===
        private static void AI_Build(FactionType faction, FactionData fd, CampaignManager cm, AIPersonality p)
        {
            if (fd.gold < 300f) return;

            // Find provinces without key buildings
            foreach (string pid in fd.ownedProvinceIds)
            {
                if (!cm.Provinces.ContainsKey(pid)) continue;
                var prov = cm.Provinces[pid];
                if (fd.gold < 200f) break;

                // Priority: Barracks (military) > Farm (food) > Market (gold) > Mine (iron)
                bool hasBarracks = false, hasFarm = false, hasMarket = false;
                foreach (var b in prov.buildings)
                {
                    if (b.type == BuildingType.Barracks && b.level > 0) hasBarracks = true;
                    if (b.type == BuildingType.Farm && b.level > 0) hasFarm = true;
                    if (b.type == BuildingType.Market && b.level > 0) hasMarket = true;
                }

                BuildingType? toBuild = null;
                if (!hasBarracks && p.aggressiveness > 0.5f) toBuild = BuildingType.Barracks;
                else if (!hasFarm && p.economy > 0.4f) toBuild = BuildingType.Farm;
                else if (!hasMarket && p.economy > 0.5f) toBuild = BuildingType.Market;

                if (toBuild != null)
                {
                    var slot = System.Array.Find(prov.buildings, b => b.type == toBuild.Value);
                    if (slot != null && !slot.isConstructing && slot.level < 3)
                    {
                        int cost = BuildingInfo.GetCostGold(toBuild.Value, slot.level + 1);
                        if (fd.gold >= cost)
                        {
                            fd.gold -= cost;
                            slot.isConstructing = true;
                            slot.turnsToComplete = BuildingInfo.GetBuildTime(toBuild.Value);
                        }
                    }
                }
            }
        }

        // === ARMY MOVEMENT ===
        private static void AI_MoveArmies(FactionType faction, FactionData fd, CampaignManager cm, AIPersonality p)
        {
            foreach (string armyId in new List<string>(fd.armyIds))
            {
                if (!cm.Armies.ContainsKey(armyId)) continue;
                var army = cm.Armies[armyId];
                if (army.movementPoints <= 0) continue;

                string currentProv = army.currentProvinceId;
                if (!cm.Provinces.ContainsKey(currentProv)) continue;
                var prov = cm.Provinces[currentProv];

                // Determine goal
                string target = null;

                // 1. If at war, move toward enemy provinces
                foreach (var rel in fd.relations)
                {
                    if (rel.Value.state != DiplomacyState.War) continue;
                    FactionType enemy = rel.Key;

                    // Find closest enemy province adjacent to current position
                    foreach (string neighbor in prov.neighborIds)
                    {
                        if (!cm.Provinces.ContainsKey(neighbor)) continue;
                        if (cm.Provinces[neighbor].owner == enemy)
                        {
                            target = neighbor;
                            break;
                        }
                    }
                    if (target != null) break;
                }

                // 2. If defensive and enemy nearby, defend
                if (target == null && p.defensiveness > 0.6f)
                {
                    foreach (string neighbor in prov.neighborIds)
                    {
                        if (!cm.Provinces.ContainsKey(neighbor)) continue;
                        var nProv = cm.Provinces[neighbor];
                        if (nProv.owner != faction && fd.relations.ContainsKey(nProv.owner) &&
                            fd.relations[nProv.owner].state == DiplomacyState.War)
                        {
                            // Stay and defend
                            target = null;
                            break;
                        }
                    }
                }

                // 3. Random patrol if nothing to do
                if (target == null && Random.value < 0.2f && prov.neighborIds.Length > 0)
                {
                    // Move to a random owned neighbor for repositioning
                    var ownedNeighbors = prov.neighborIds.Where(
                        n => cm.Provinces.ContainsKey(n) && cm.Provinces[n].owner == faction).ToArray();
                    if (ownedNeighbors.Length > 0)
                        target = ownedNeighbors[Random.Range(0, ownedNeighbors.Length)];
                }

                if (target != null && army.movementPoints > 0)
                {
                    army.currentProvinceId = target;
                    army.movementPoints--;
                }
            }
        }

        // === DIPLOMACY ===
        private static void AI_Diplomacy(FactionType faction, FactionData fd, CampaignManager cm, AIPersonality p)
        {
            foreach (var other in cm.Factions.Keys)
            {
                if (other == faction) continue;
                var otherFd = cm.Factions[other];
                if (otherFd.isEliminated) continue;

                // Consider war declaration
                if (fd.relations.ContainsKey(other) && fd.relations[other].state == DiplomacyState.Neutral)
                {
                    float warDesire = 0f;
                    warDesire += p.aggressiveness * 30f;
                    warDesire += p.expansionism * 20f;
                    warDesire -= p.diplomacy * 25f;

                    // More likely if we're much stronger
                    float ourStrength = fd.ownedProvinceIds.Count * 100f + fd.manpower * 0.1f;
                    float theirStrength = otherFd.ownedProvinceIds.Count * 100f + otherFd.manpower * 0.1f;
                    if (ourStrength > theirStrength * 2f) warDesire += 20f;
                    if (ourStrength < theirStrength * 0.5f) warDesire -= 40f;

                    // Less likely if already at war with someone
                    int activeWars = fd.relations.Count(r => r.Value.state == DiplomacyState.War);
                    warDesire -= activeWars * 20f;

                    // Check if we have a CB
                    if (WarJustificationSystem.HasCasusBelli(faction, other))
                        warDesire += 15f;

                    if (warDesire > 50f && fd.warSupport > 0.3f)
                    {
                        // Declare war!
                        fd.relations[other].state = DiplomacyState.War;
                        if (otherFd.relations.ContainsKey(faction))
                            otherFd.relations[faction].state = DiplomacyState.War;
                        cm.AddFactionEvent(faction, $"{faction} déclare la guerre à {other}!");
                        cm.AddFactionEvent(other, $"{faction} nous déclare la guerre!");
                    }
                }

                // Consider peace if losing badly
                if (fd.relations.ContainsKey(other) && fd.relations[other].state == DiplomacyState.War)
                {
                    float warScore = WarJustificationSystem.GetWarScore(other, faction);
                    if (warScore > 60f && fd.warSupport < 0.2f)
                    {
                        // Sue for peace
                        fd.relations[other].state = DiplomacyState.Neutral;
                        if (otherFd.relations.ContainsKey(faction))
                            otherFd.relations[faction].state = DiplomacyState.Neutral;
                        cm.AddFactionEvent(faction, $"Paix signée avec {other}");
                    }
                }
            }

            // Start CB fabrication against potential enemies
            if (Random.value < p.aggressiveness * 0.15f)
            {
                // Find a neighbor we don't like
                foreach (string pid in fd.ownedProvinceIds)
                {
                    if (!cm.Provinces.ContainsKey(pid)) continue;
                    foreach (string neighbor in cm.Provinces[pid].neighborIds)
                    {
                        if (!cm.Provinces.ContainsKey(neighbor)) continue;
                        FactionType neighborOwner = cm.Provinces[neighbor].owner;
                        if (neighborOwner == faction) continue;
                        if (fd.relations.ContainsKey(neighborOwner) && fd.relations[neighborOwner].state == DiplomacyState.War) continue;

                        if (!WarJustificationSystem.HasCasusBelli(faction, neighborOwner) &&
                            WarJustificationSystem.GetActiveJustifications(faction).Count < 2)
                        {
                            WarJustificationSystem.StartJustification(faction, neighborOwner, CasusBelliType.Conquest);
                            return;
                        }
                    }
                }
            }
        }

        // === BUDGET ===
        private static void AI_AdjustBudget(FactionType faction, AIPersonality p)
        {
            var budget = EconomyManager.GetBudget(faction);
            budget.military = 0.15f + p.aggressiveness * 0.25f;
            budget.navy = p.naval * 0.20f;
            budget.administration = 0.15f;
            budget.infrastructure = p.economy * 0.20f;
            budget.research = 0.15f + p.economy * 0.10f;
            budget.Normalize();
        }

        // === LAWS ===
        private static void AI_ChangeLaws(FactionType faction, FactionData fd, AIPersonality p)
        {
            // Only change laws if we have enough PP
            if (fd.politicalPower < 100f) return;

            // Aggressive nations escalate conscription during war
            int activeWars = fd.relations.Count(r => r.Value.state == DiplomacyState.War);
            if (activeWars > 0 && p.aggressiveness > 0.5f)
            {
                if (fd.conscriptionLaw < ConscriptionLaw.ExtendedConscription)
                    NationalLaws.TryChangeConscription(fd, fd.conscriptionLaw + 1);

                if (fd.economyLaw < EconomyLaw.WarEconomy)
                    NationalLaws.TryChangeEconomy(fd, fd.economyLaw + 1);
            }

            // Economic nations improve trade
            if (p.economy > 0.7f && fd.tradeLaw != TradeLaw.ExportFocus)
                NationalLaws.TryChangeTrade(fd, TradeLaw.ExportFocus);
        }
    }
}
