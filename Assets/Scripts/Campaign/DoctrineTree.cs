using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// HoI4-style military doctrine trees. 4 exclusive paths — choosing one locks the others.
    /// Each doctrine provides cumulative bonuses as you research deeper.
    /// </summary>

    public enum DoctrineType
    {
        None,
        GrandeBatterie,     // Artillery-focused: +soft attack, +breakthrough
        GuerreDeMouvement,  // Mobility-focused: +speed, +org recovery
        DoctrineDefensive,  // Fortification-focused: +defense, +entrenchment
        DoctrineDeLaMasse,  // Numbers-focused: +manpower, +reinforcement rate
        DoctrineNavale      // Naval-focused: +naval combat, +blockade, +amphibious
    }

    [System.Serializable]
    public class DoctrineNode
    {
        public string nodeId;
        public string displayName;
        public string description;
        public int depth;  // How deep in the tree (0 = root)
        public bool isResearched = false;
        public int researchCost = 5;  // Turns
        public string[] prerequisites = new string[0]; // Required nodes
        public string[] mutuallyExclusive = new string[0]; // Can't research if these are done

        // Bonuses
        public float attackBonus;
        public float defenseBonus;
        public float speedBonus;
        public float orgBonus;
        public float manpowerModifier;
        public float entrenchmentBonus;
        public float reconBonus;        // Reconnaissance bonus
        public float nightCombatBonus;
        public float amphibiousBonus;
        public float supplyEfficiency;
        public float moraleBonus;
        public float navalAttackBonus;
        public float navalDefenseBonus;
        public float blockadeEfficiency;

        public DoctrineNode(string id, string name, string desc, int depth, int cost)
        {
            nodeId = id;
            displayName = name;
            description = desc;
            this.depth = depth;
            researchCost = cost;
        }
    }

    public static class DoctrineTree
    {
        private static Dictionary<FactionType, DoctrineType> chosenDoctrine = 
            new Dictionary<FactionType, DoctrineType>();
        
        private static Dictionary<DoctrineType, List<DoctrineNode>> doctrineTrees =
            new Dictionary<DoctrineType, List<DoctrineNode>>();

        static DoctrineTree()
        {
            InitializeTrees();
        }

        private static void InitializeTrees()
        {
            // === GRANDE BATTERIE (8 nodes, branch at depth 4) ===
            doctrineTrees[DoctrineType.GrandeBatterie] = new List<DoctrineNode>
            {
                new DoctrineNode("gb_1", "Concentration d'artillerie", "+10% attaque, masser les canons", 0, 4)
                    { attackBonus = 0.10f },
                new DoctrineNode("gb_2", "Tir coordonné", "+10% percée, tir de batterie synchronisé", 1, 5)
                    { attackBonus = 0.10f, prerequisites = new[] { "gb_1" } },
                new DoctrineNode("gb_3", "Barrage préparatoire", "+15% attaque, +5% org, bombardement avant assaut", 2, 5)
                    { attackBonus = 0.15f, orgBonus = 5f, prerequisites = new[] { "gb_2" } },
                new DoctrineNode("gb_4a", "Barrage roulant", "+20% attaque, avance derrière le feu", 3, 6)
                    { attackBonus = 0.20f, speedBonus = 0.05f,
                      prerequisites = new[] { "gb_3" }, mutuallyExclusive = new[] { "gb_4b" } },
                new DoctrineNode("gb_4b", "Siège scientifique", "+15% défense, +20% entrenchment, génie de siège", 3, 6)
                    { defenseBonus = 0.15f, entrenchmentBonus = 0.20f,
                      prerequisites = new[] { "gb_3" }, mutuallyExclusive = new[] { "gb_4a" } },
                new DoctrineNode("gb_5", "Artillerie à cheval", "+10% vitesse, canons mobiles d'élite", 4, 6)
                    { speedBonus = 0.10f, attackBonus = 0.05f, prerequisites = new[] { "gb_4a" } },
                new DoctrineNode("gb_6", "Destruction systématique", "+10% attaque, cibler les positions clés", 5, 7)
                    { attackBonus = 0.10f, moraleBonus = 0.10f, prerequisites = new[] { "gb_4b" } },
                new DoctrineNode("gb_7", "Batterie impériale", "+15% attaque, +10% org, artillerie suprême", 6, 8)
                    { attackBonus = 0.15f, orgBonus = 10f, moraleBonus = 0.05f,
                      prerequisites = new[] { "gb_5", "gb_6" } },
            };

            // === GUERRE DE MOUVEMENT (8 nodes, branch at depth 4) ===
            doctrineTrees[DoctrineType.GuerreDeMouvement] = new List<DoctrineNode>
            {
                new DoctrineNode("gm_1", "Cavalerie d'exploitation", "+10% vitesse, cavalerie rapide", 0, 4)
                    { speedBonus = 0.10f },
                new DoctrineNode("gm_2", "Encerclement tactique", "+10% org, manœuvres d'enveloppement", 1, 5)
                    { orgBonus = 10f, prerequisites = new[] { "gm_1" } },
                new DoctrineNode("gm_3", "Reconnaissance avancée", "+15% recon, repérer les faiblesses", 2, 5)
                    { reconBonus = 0.15f, speedBonus = 0.05f, prerequisites = new[] { "gm_2" } },
                new DoctrineNode("gm_4a", "Poursuite impitoyable", "+15% vitesse, +10% attaque, écraser la retraite", 3, 6)
                    { speedBonus = 0.15f, attackBonus = 0.10f,
                      prerequisites = new[] { "gm_3" }, mutuallyExclusive = new[] { "gm_4b" } },
                new DoctrineNode("gm_4b", "Combat nocturne", "+15% nuit, +10% org, attaquer de nuit", 3, 6)
                    { nightCombatBonus = 0.15f, orgBonus = 10f,
                      prerequisites = new[] { "gm_3" }, mutuallyExclusive = new[] { "gm_4a" } },
                new DoctrineNode("gm_5", "Marche forcée", "+10% vitesse, +5% supply, marches rapides", 4, 6)
                    { speedBonus = 0.10f, supplyEfficiency = 0.05f, prerequisites = new[] { "gm_4a" } },
                new DoctrineNode("gm_6", "Raids profonds", "+10% recon, +10% attaque, raids derrière les lignes", 5, 7)
                    { reconBonus = 0.10f, attackBonus = 0.10f, prerequisites = new[] { "gm_4b" } },
                new DoctrineNode("gm_7", "Manœuvre stratégique", "+15% org, +10% vitesse, maître de la guerre mobile", 6, 8)
                    { orgBonus = 15f, speedBonus = 0.10f, moraleBonus = 0.10f,
                      prerequisites = new[] { "gm_5", "gm_6" } },
            };

            // === DOCTRINE DÉFENSIVE (8 nodes, branch at depth 4) ===
            doctrineTrees[DoctrineType.DoctrineDefensive] = new List<DoctrineNode>
            {
                new DoctrineNode("dd_1", "Fortifications avancées", "+10% défense, renforcer les positions", 0, 4)
                    { defenseBonus = 0.10f },
                new DoctrineNode("dd_2", "Défense en profondeur", "+10% défense, +10% entrenchment, lignes multiples", 1, 5)
                    { defenseBonus = 0.10f, entrenchmentBonus = 0.10f, prerequisites = new[] { "dd_1" } },
                new DoctrineNode("dd_3", "Contre-attaque planifiée", "+10% attaque, +5% org, frapper au bon moment", 2, 5)
                    { attackBonus = 0.10f, orgBonus = 5f, prerequisites = new[] { "dd_2" } },
                new DoctrineNode("dd_4a", "Dernier carré", "+20% défense, +15% morale, ne jamais céder", 3, 6)
                    { defenseBonus = 0.20f, moraleBonus = 0.15f,
                      prerequisites = new[] { "dd_3" }, mutuallyExclusive = new[] { "dd_4b" } },
                new DoctrineNode("dd_4b", "Guerre d'attrition", "+10% défense, +15% supply, épuiser l'ennemi", 3, 6)
                    { defenseBonus = 0.10f, supplyEfficiency = 0.15f,
                      prerequisites = new[] { "dd_3" }, mutuallyExclusive = new[] { "dd_4a" } },
                new DoctrineNode("dd_5", "Discipline de fer", "+10% morale, +5% org, discipline inébranlable", 4, 6)
                    { moraleBonus = 0.10f, orgBonus = 5f, prerequisites = new[] { "dd_4a" } },
                new DoctrineNode("dd_6", "Terre brûlée", "+10% défense, détruire ce qu'on ne peut tenir", 5, 7)
                    { defenseBonus = 0.10f, supplyEfficiency = 0.10f, prerequisites = new[] { "dd_4b" } },
                new DoctrineNode("dd_7", "Forteresse nationale", "+15% défense, +10% org, nation imprenable", 6, 8)
                    { defenseBonus = 0.15f, orgBonus = 10f, entrenchmentBonus = 0.15f,
                      prerequisites = new[] { "dd_5", "dd_6" } },
            };

            // === DOCTRINE DE LA MASSE (8 nodes, branch at depth 4) ===
            doctrineTrees[DoctrineType.DoctrineDeLaMasse] = new List<DoctrineNode>
            {
                new DoctrineNode("dm_1", "Mobilisation populaire", "+10% manpower, appeler les réserves", 0, 4)
                    { manpowerModifier = 0.10f },
                new DoctrineNode("dm_2", "Levée en masse", "+10% manpower, +5% org, conscription nationale", 1, 5)
                    { manpowerModifier = 0.10f, orgBonus = 5f, prerequisites = new[] { "dm_1" } },
                new DoctrineNode("dm_3", "Formation accélérée", "+5% attaque, +5% défense, entraînement rapide", 2, 5)
                    { attackBonus = 0.05f, defenseBonus = 0.05f, prerequisites = new[] { "dm_2" } },
                new DoctrineNode("dm_4a", "Vague humaine", "+10% attaque, +15% manpower, submerger l'ennemi", 3, 6)
                    { attackBonus = 0.10f, manpowerModifier = 0.15f,
                      prerequisites = new[] { "dm_3" }, mutuallyExclusive = new[] { "dm_4b" } },
                new DoctrineNode("dm_4b", "Guérilla populaire", "+15% défense, +10% recon, résistance partisane", 3, 6)
                    { defenseBonus = 0.15f, reconBonus = 0.10f,
                      prerequisites = new[] { "dm_3" }, mutuallyExclusive = new[] { "dm_4a" } },
                new DoctrineNode("dm_5", "Renforts continus", "+10% manpower, +5% org, flux constant", 4, 6)
                    { manpowerModifier = 0.10f, orgBonus = 5f, prerequisites = new[] { "dm_4a" } },
                new DoctrineNode("dm_6", "Milice territoriale", "+10% défense, +10% manpower, défense locale", 5, 7)
                    { defenseBonus = 0.10f, manpowerModifier = 0.10f, prerequisites = new[] { "dm_4b" } },
                new DoctrineNode("dm_7", "Nation en armes", "+15% manpower, +10% attaque, +5% morale, peuple combattant", 6, 8)
                    { manpowerModifier = 0.15f, attackBonus = 0.10f, moraleBonus = 0.05f,
                      prerequisites = new[] { "dm_5", "dm_6" } },
            };

            // === DOCTRINE NAVALE (8 nodes, branch at depth 4) ===
            doctrineTrees[DoctrineType.DoctrineNavale] = new List<DoctrineNode>
            {
                new DoctrineNode("dn_1", "Suprématie des mers", "+10% naval attaque, dominer les océans", 0, 4)
                    { navalAttackBonus = 0.10f },
                new DoctrineNode("dn_2", "Ligne de bataille", "+10% naval défense, formation en ligne", 1, 5)
                    { navalDefenseBonus = 0.10f, prerequisites = new[] { "dn_1" } },
                new DoctrineNode("dn_3", "Blocus stratégique", "+15% blocus, couper le commerce ennemi", 2, 5)
                    { blockadeEfficiency = 0.15f, prerequisites = new[] { "dn_2" } },
                new DoctrineNode("dn_4a", "Bataille décisive", "+20% naval attaque, chercher la bataille navale", 3, 6)
                    { navalAttackBonus = 0.20f,
                      prerequisites = new[] { "dn_3" }, mutuallyExclusive = new[] { "dn_4b" } },
                new DoctrineNode("dn_4b", "Guerre de course", "+15% blocus, +10% recon, corsaires et frégates", 3, 6)
                    { blockadeEfficiency = 0.15f, reconBonus = 0.10f,
                      prerequisites = new[] { "dn_3" }, mutuallyExclusive = new[] { "dn_4a" } },
                new DoctrineNode("dn_5", "Débarquement amphibie", "+15% amphibie, invasion par la mer", 4, 6)
                    { amphibiousBonus = 0.15f, prerequisites = new[] { "dn_4a" } },
                new DoctrineNode("dn_6", "Convois protégés", "+10% supply naval, +10% naval défense", 5, 7)
                    { supplyEfficiency = 0.10f, navalDefenseBonus = 0.10f, prerequisites = new[] { "dn_4b" } },
                new DoctrineNode("dn_7", "Thalassocratie", "+15% naval, +10% blocus, maître des océans", 6, 8)
                    { navalAttackBonus = 0.15f, blockadeEfficiency = 0.10f, moraleBonus = 0.10f,
                      prerequisites = new[] { "dn_5", "dn_6" } },
            };
        }

        public static DoctrineType GetChosenDoctrine(FactionType faction)
        {
            return chosenDoctrine.ContainsKey(faction) ? chosenDoctrine[faction] : DoctrineType.None;
        }

        public static bool ChooseDoctrine(FactionType faction, DoctrineType doctrine)
        {
            if (chosenDoctrine.ContainsKey(faction) && chosenDoctrine[faction] != DoctrineType.None)
                return false; // Already chosen — cannot change
            chosenDoctrine[faction] = doctrine;
            return true;
        }

        public static List<DoctrineNode> GetDoctrineNodes(DoctrineType type)
        {
            return doctrineTrees.ContainsKey(type) ? doctrineTrees[type] : new List<DoctrineNode>();
        }

        /// <summary>Get total cumulative bonuses from researched doctrine nodes</summary>
        public static (float attack, float defense, float speed, float org, float manpower) GetTotalBonuses(FactionType faction)
        {
            var doctrine = GetChosenDoctrine(faction);
            if (doctrine == DoctrineType.None) return (0, 0, 0, 0, 0);

            float a = 0, d = 0, s = 0, o = 0, m = 0;
            var nodes = GetDoctrineNodes(doctrine);
            foreach (var node in nodes)
            {
                if (node.isResearched)
                {
                    a += node.attackBonus;
                    d += node.defenseBonus;
                    s += node.speedBonus;
                    o += node.orgBonus;
                    m += node.manpowerModifier;
                }
            }
            return (a, d, s, o, m);
        }

        /// <summary>Get extended bonuses including new fields</summary>
        public static DoctrineBonus GetExtendedBonuses(FactionType faction)
        {
            var bonus = new DoctrineBonus();
            var doctrine = GetChosenDoctrine(faction);
            if (doctrine == DoctrineType.None) return bonus;

            var nodes = GetDoctrineNodes(doctrine);
            foreach (var node in nodes)
            {
                if (!node.isResearched) continue;
                bonus.attack += node.attackBonus;
                bonus.defense += node.defenseBonus;
                bonus.speed += node.speedBonus;
                bonus.org += node.orgBonus;
                bonus.manpower += node.manpowerModifier;
                bonus.entrenchment += node.entrenchmentBonus;
                bonus.recon += node.reconBonus;
                bonus.nightCombat += node.nightCombatBonus;
                bonus.amphibious += node.amphibiousBonus;
                bonus.supply += node.supplyEfficiency;
                bonus.morale += node.moraleBonus;
                bonus.navalAttack += node.navalAttackBonus;
                bonus.navalDefense += node.navalDefenseBonus;
                bonus.blockade += node.blockadeEfficiency;
            }
            return bonus;
        }

        /// <summary>Research a doctrine node, checking prerequisites and mutual exclusions</summary>
        public static bool ResearchNode(FactionType faction, string nodeId)
        {
            var doctrine = GetChosenDoctrine(faction);
            if (doctrine == DoctrineType.None) return false;

            var nodes = GetDoctrineNodes(doctrine);
            var node = nodes.Find(n => n.nodeId == nodeId);
            if (node == null || node.isResearched) return false;

            // Check prerequisites
            foreach (string prereq in node.prerequisites)
            {
                var prereqNode = nodes.Find(n => n.nodeId == prereq);
                if (prereqNode == null || !prereqNode.isResearched) return false;
            }

            // Check mutual exclusions
            if (node.mutuallyExclusive != null)
            {
                foreach (string excl in node.mutuallyExclusive)
                {
                    var exclNode = nodes.Find(n => n.nodeId == excl);
                    if (exclNode != null && exclNode.isResearched) return false;
                }
            }

            node.isResearched = true;
            return true;
        }

        /// <summary>Get researched node count for a faction</summary>
        public static int GetResearchedCount(FactionType faction)
        {
            var doctrine = GetChosenDoctrine(faction);
            if (doctrine == DoctrineType.None) return 0;
            int count = 0;
            foreach (var node in GetDoctrineNodes(doctrine))
                if (node.isResearched) count++;
            return count;
        }

        public static string GetDoctrineName(DoctrineType type) => type switch
        {
            DoctrineType.GrandeBatterie => "Grande Batterie",
            DoctrineType.GuerreDeMouvement => "Guerre de Mouvement",
            DoctrineType.DoctrineDefensive => "Doctrine Défensive",
            DoctrineType.DoctrineDeLaMasse => "Doctrine de la Masse",
            DoctrineType.DoctrineNavale => "Doctrine Navale",
            _ => "Aucune"
        };

        public static string GetDoctrineDescription(DoctrineType type) => type switch
        {
            DoctrineType.GrandeBatterie => "Concentrer la puissance de feu de l'artillerie pour écraser l'ennemi",
            DoctrineType.GuerreDeMouvement => "Vaincre par la vitesse, l'encerclement et la surprise",
            DoctrineType.DoctrineDefensive => "Tenir des positions imprenables et contre-attaquer au bon moment",
            DoctrineType.DoctrineDeLaMasse => "Submerger l'ennemi par le nombre et la mobilisation totale",
            DoctrineType.DoctrineNavale => "Dominer les mers par la puissance navale et le blocus",
            _ => ""
        };
    }

    /// <summary>Struct holding all cumulative doctrine bonuses</summary>
    public struct DoctrineBonus
    {
        public float attack, defense, speed, org, manpower;
        public float entrenchment, recon, nightCombat, amphibious;
        public float supply, morale;
        public float navalAttack, navalDefense, blockade;
    }
}
