using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// General/Commander data for campaign. Generals lead armies and provide bonuses.
    /// </summary>
    [System.Serializable]
    public class GeneralData
    {
        public string generalId;
        public string firstName;
        public string lastName;
        public string FullName => $"{firstName} {lastName}";
        
        public FactionType faction;
        public string assignedArmyId;
        public bool isRuler;           // King/Emperor
        public bool isHeir;            // Heir to throne
        
        // Stats (1-10)
        public int command = 5;        // Battle effectiveness
        public int authority = 5;      // Morale bonus
        public int charisma = 5;       // Diplomacy bonus
        public int intelligence = 5;   // Research/strategy
        
        // Experience
        public int level = 1;
        public int experience = 0;
        public int skillPoints = 0;
        
        // Traits
        public List<CharacterTrait> traits = new List<CharacterTrait>();
        
        // Skills (unlocked via skill tree)
        public List<string> unlockedSkills = new List<string>();
        
        // Personal
        public int age = 35;
        public int birthYear = 1770;
        public bool isAlive = true;
        
        // Family
        public string spouseId;
        public List<string> childrenIds = new List<string>();
        public string fatherId;
        
        public GeneralData(string firstName, string lastName, FactionType faction)
        {
            this.generalId = System.Guid.NewGuid().ToString();
            this.firstName = firstName;
            this.lastName = lastName;
            this.faction = faction;
        }

        #region Stats & Bonuses

        public float GetMoraleBonus()
        {
            float bonus = authority * 2f;
            
            foreach (var trait in traits)
            {
                bonus += trait.moraleBonus;
            }
            
            if (unlockedSkills.Contains("inspiring_presence"))
                bonus += 10f;
            
            return bonus;
        }

        public float GetAttackBonus()
        {
            float bonus = command * 0.02f; // 2% per command point
            
            foreach (var trait in traits)
            {
                bonus += trait.attackBonus;
            }
            
            if (unlockedSkills.Contains("aggressive_tactics"))
                bonus += 0.1f;
            
            return bonus;
        }

        public float GetDefenseBonus()
        {
            float bonus = command * 0.015f;
            
            foreach (var trait in traits)
            {
                bonus += trait.defenseBonus;
            }
            
            if (unlockedSkills.Contains("defensive_master"))
                bonus += 0.1f;
            
            return bonus;
        }

        public float GetMovementBonus()
        {
            float bonus = 0f;
            
            foreach (var trait in traits)
            {
                bonus += trait.movementBonus;
            }
            
            if (unlockedSkills.Contains("forced_march"))
                bonus += 1f;
            
            return bonus;
        }

        public float GetDiplomacyBonus()
        {
            return charisma * 5f;
        }

        #endregion

        #region Experience & Leveling

        public void AddExperience(int amount)
        {
            experience += amount;
            
            int expForNextLevel = GetExpForLevel(level + 1);
            while (experience >= expForNextLevel && level < 10)
            {
                level++;
                skillPoints++;
                experience -= expForNextLevel;
                expForNextLevel = GetExpForLevel(level + 1);
                
                Debug.Log($"[General] {FullName} reached level {level}!");
            }
        }

        private int GetExpForLevel(int lvl)
        {
            return lvl * lvl * 100;
        }

        public void UnlockSkill(string skillId)
        {
            if (skillPoints <= 0) return;
            if (unlockedSkills.Contains(skillId)) return;
            
            var skill = GeneralSkillTree.GetSkill(skillId);
            if (skill == null) return;
            
            // Check prerequisites
            foreach (var prereq in skill.prerequisites)
            {
                if (!unlockedSkills.Contains(prereq))
                    return;
            }
            
            unlockedSkills.Add(skillId);
            skillPoints--;
            
            Debug.Log($"[General] {FullName} unlocked skill: {skill.name}");
        }

        #endregion

        #region Traits

        public void AddTrait(CharacterTrait trait)
        {
            // Check for conflicting traits
            foreach (var existing in traits)
            {
                if (existing.IsConflicting(trait))
                    return;
            }
            
            traits.Add(trait);
            Debug.Log($"[General] {FullName} gained trait: {trait.name}");
        }

        public void RemoveTrait(string traitId)
        {
            traits.RemoveAll(t => t.traitId == traitId);
        }

        public bool HasTrait(string traitId)
        {
            return traits.Exists(t => t.traitId == traitId);
        }

        #endregion

        #region Battle Events

        public void OnBattleWon(int enemiesKilled, bool wasDefending)
        {
            AddExperience(50 + enemiesKilled / 10);
            
            // Chance to gain positive trait
            if (Random.value < 0.1f)
            {
                if (wasDefending && !HasTrait("stalwart"))
                    AddTrait(CharacterTrait.GetTrait("stalwart"));
                else if (!wasDefending && !HasTrait("aggressive"))
                    AddTrait(CharacterTrait.GetTrait("aggressive"));
            }
        }

        public void OnBattleLost()
        {
            AddExperience(20);
            
            // Chance to gain negative trait
            if (Random.value < 0.05f && !HasTrait("cautious"))
            {
                AddTrait(CharacterTrait.GetTrait("cautious"));
            }
        }

        public void OnKilledInBattle()
        {
            isAlive = false;
            Debug.Log($"[General] {FullName} was killed in battle!");
        }

        #endregion

        #region Static Generators

        public static GeneralData CreateHistoricalGeneral(string name, FactionType faction, int cmd, int auth, int cha)
        {
            string[] parts = name.Split(' ');
            string first = parts[0];
            string last = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : "";
            
            var general = new GeneralData(first, last, faction)
            {
                command = cmd,
                authority = auth,
                charisma = cha,
                level = Mathf.Max(1, (cmd + auth + cha) / 6)
            };
            
            AssignStartingTraits(general);
            return general;
        }

        public static GeneralData CreateRandomGeneral(FactionType faction)
        {
            string firstName = GetRandomFirstName(faction);
            string lastName = GetRandomLastName(faction);
            
            var general = new GeneralData(firstName, lastName, faction)
            {
                command = Random.Range(3, 8),
                authority = Random.Range(3, 8),
                charisma = Random.Range(3, 8),
                intelligence = Random.Range(3, 8),
                age = Random.Range(25, 55)
            };
            
            AssignStartingTraits(general);
            return general;
        }
        
        /// <summary>Auto-assign traits based on stats at creation.</summary>
        private static void AssignStartingTraits(GeneralData gen)
        {
            // High command → offensive or defensive trait
            if (gen.command >= 8)
                gen.AddTrait(CharacterTrait.GetTrait(Random.value > 0.5f ? "aggressive" : "brilliant"));
            else if (gen.command >= 7)
                gen.AddTrait(CharacterTrait.GetTrait(Random.value > 0.5f ? "cautious" : "stalwart"));

            // High authority → inspiring leader
            if (gen.authority >= 8)
                gen.AddTrait(CharacterTrait.GetTrait("inspiring"));
            else if (gen.authority >= 7)
                gen.AddTrait(CharacterTrait.GetTrait("brave"));

            // High charisma → diplomat
            if (gen.charisma >= 7)
                gen.AddTrait(CharacterTrait.GetTrait("diplomat"));

            // High intelligence → reformer
            if (gen.intelligence >= 7)
                gen.AddTrait(CharacterTrait.GetTrait("reformer"));
            
            // Random bonus trait (10% chance each)
            if (Random.value < 0.10f) gen.AddTrait(CharacterTrait.GetTrait("siege_master"));
            if (Random.value < 0.10f) gen.AddTrait(CharacterTrait.GetTrait("mountaineer"));
            if (Random.value < 0.08f) gen.AddTrait(CharacterTrait.GetTrait("night_owl"));
            if (Random.value < 0.05f) gen.AddTrait(CharacterTrait.GetTrait("old_guard"));
            if (Random.value < 0.05f) gen.AddTrait(CharacterTrait.GetTrait("organizer"));
            if (Random.value < 0.04f) gen.AddTrait(CharacterTrait.GetTrait("drunkard"));
        }

        private static string GetRandomFirstName(FactionType faction)
        {
            switch (faction)
            {
                case FactionType.France:
                    return new[] { "Jean", "Pierre", "Louis", "François", "Michel", "Henri" }[Random.Range(0, 6)];
                case FactionType.Britain:
                    return new[] { "William", "John", "Thomas", "James", "George", "Charles" }[Random.Range(0, 6)];
                case FactionType.Prussia:
                    return new[] { "Friedrich", "Wilhelm", "Karl", "Heinrich", "Ludwig", "Otto" }[Random.Range(0, 6)];
                case FactionType.Russia:
                    return new[] { "Alexander", "Mikhail", "Nikolai", "Pavel", "Dmitri", "Ivan" }[Random.Range(0, 6)];
                case FactionType.Austria:
                    return new[] { "Franz", "Johann", "Leopold", "Karl", "Ferdinand", "Joseph" }[Random.Range(0, 6)];
                case FactionType.Spain:
                    return new[] { "Carlos", "Fernando", "Miguel", "Antonio", "José", "Pedro" }[Random.Range(0, 6)];
                case FactionType.Ottoman:
                    return new[] { "Mehmed", "Mustafa", "Ahmed", "Selim", "Mahmud", "Osman" }[Random.Range(0, 6)];
                default:
                    return "Unknown";
            }
        }

        private static string GetRandomLastName(FactionType faction)
        {
            switch (faction)
            {
                case FactionType.France:
                    return new[] { "Dupont", "Martin", "Bernard", "Moreau", "Laurent", "Lefebvre" }[Random.Range(0, 6)];
                case FactionType.Britain:
                    return new[] { "Smith", "Wellington", "Nelson", "Moore", "Hill", "Graham" }[Random.Range(0, 6)];
                case FactionType.Prussia:
                    return new[] { "von Blücher", "von Gneisenau", "von Scharnhorst", "von Bülow", "von Yorck", "von Kleist" }[Random.Range(0, 6)];
                case FactionType.Russia:
                    return new[] { "Kutuzov", "Bagration", "Barclay", "Bennigsen", "Wittgenstein", "Platov" }[Random.Range(0, 6)];
                case FactionType.Austria:
                    return new[] { "von Habsburg", "Schwarzenberg", "Radetzky", "Mack", "Hiller", "Bellegarde" }[Random.Range(0, 6)];
                case FactionType.Spain:
                    return new[] { "de la Romana", "Castaños", "Blake", "Cuesta", "Palafox", "Ballesteros" }[Random.Range(0, 6)];
                case FactionType.Ottoman:
                    return new[] { "Pasha", "Bey", "Aga", "Efendi", "Çelebi", "Reis" }[Random.Range(0, 6)];
                default:
                    return "Unknown";
            }
        }

        #endregion
    }

    /// <summary>
    /// Character trait that provides bonuses/penalties
    /// </summary>
    [System.Serializable]
    public class CharacterTrait
    {
        public string traitId;
        public string name;
        public string description;
        public bool isPositive;
        
        // Bonuses
        public float moraleBonus;
        public float attackBonus;
        public float defenseBonus;
        public float movementBonus;
        
        // Conflicts
        public List<string> conflictingTraits = new List<string>();

        public bool IsConflicting(CharacterTrait other)
        {
            return conflictingTraits.Contains(other.traitId) || 
                   other.conflictingTraits.Contains(traitId);
        }

        public static CharacterTrait GetTrait(string traitId)
        {
            switch (traitId)
            {
                case "brave":
                    return new CharacterTrait
                    {
                        traitId = "brave",
                        name = "Brave",
                        description = "Inspires troops with personal courage",
                        isPositive = true,
                        moraleBonus = 10f,
                        attackBonus = 0.05f,
                        conflictingTraits = new List<string> { "coward" }
                    };
                    
                case "coward":
                    return new CharacterTrait
                    {
                        traitId = "coward",
                        name = "Coward",
                        description = "Tends to flee from danger",
                        isPositive = false,
                        moraleBonus = -15f,
                        conflictingTraits = new List<string> { "brave" }
                    };
                    
                case "aggressive":
                    return new CharacterTrait
                    {
                        traitId = "aggressive",
                        name = "Aggressive",
                        description = "Favors offensive tactics",
                        isPositive = true,
                        attackBonus = 0.1f,
                        defenseBonus = -0.05f,
                        conflictingTraits = new List<string> { "cautious" }
                    };
                    
                case "cautious":
                    return new CharacterTrait
                    {
                        traitId = "cautious",
                        name = "Cautious",
                        description = "Prefers defensive positions",
                        isPositive = true,
                        defenseBonus = 0.1f,
                        attackBonus = -0.05f,
                        conflictingTraits = new List<string> { "aggressive" }
                    };
                    
                case "stalwart":
                    return new CharacterTrait
                    {
                        traitId = "stalwart",
                        name = "Stalwart",
                        description = "Holds the line under pressure",
                        isPositive = true,
                        defenseBonus = 0.15f,
                        moraleBonus = 5f
                    };
                    
                case "brilliant":
                    return new CharacterTrait
                    {
                        traitId = "brilliant",
                        name = "Brilliant Tactician",
                        description = "Exceptional tactical mind",
                        isPositive = true,
                        attackBonus = 0.1f,
                        defenseBonus = 0.1f
                    };
                    
                case "cruel":
                    return new CharacterTrait
                    {
                        traitId = "cruel",
                        name = "Cruel",
                        description = "Feared by enemies and allies alike",
                        isPositive = false,
                        moraleBonus = -5f,
                        attackBonus = 0.05f
                    };
                    
                case "inspiring":
                    return new CharacterTrait
                    {
                        traitId = "inspiring",
                        name = "Chef Inspirant",
                        description = "Les troupes se battent plus dur sous son commandement",
                        isPositive = true,
                        moraleBonus = 15f
                    };

                case "organizer":
                    return new CharacterTrait
                    {
                        traitId = "organizer",
                        name = "Organisateur",
                        description = "Gestion logistique excellente",
                        isPositive = true,
                        movementBonus = 0.5f
                    };

                case "reckless":
                    return new CharacterTrait
                    {
                        traitId = "reckless",
                        name = "Téméraire",
                        description = "Attaque sans réfléchir, risque de pertes inutiles",
                        isPositive = false,
                        attackBonus = 0.15f,
                        defenseBonus = -0.10f,
                        conflictingTraits = new List<string> { "cautious" }
                    };

                case "siege_master":
                    return new CharacterTrait
                    {
                        traitId = "siege_master",
                        name = "Maître de Siège",
                        description = "Expert dans l'art d'assiéger les forteresses",
                        isPositive = true,
                        attackBonus = 0.05f
                    };

                case "night_owl":
                    return new CharacterTrait
                    {
                        traitId = "night_owl",
                        name = "Noctambule",
                        description = "Excelle dans les combats nocturnes",
                        isPositive = true,
                        attackBonus = 0.08f
                    };

                case "mountaineer":
                    return new CharacterTrait
                    {
                        traitId = "mountaineer",
                        name = "Montagnard",
                        description = "Excelle en terrain montagneux",
                        isPositive = true,
                        defenseBonus = 0.10f,
                        movementBonus = 0.5f
                    };

                case "winter_soldier":
                    return new CharacterTrait
                    {
                        traitId = "winter_soldier",
                        name = "Soldat de l'Hiver",
                        description = "Les troupes résistent mieux au froid",
                        isPositive = true,
                        defenseBonus = 0.05f,
                        moraleBonus = 5f
                    };

                case "diplomat":
                    return new CharacterTrait
                    {
                        traitId = "diplomat",
                        name = "Diplomate",
                        description = "Habile négociateur, bonus diplomatiques",
                        isPositive = true,
                        moraleBonus = 5f
                    };

                case "drunkard":
                    return new CharacterTrait
                    {
                        traitId = "drunkard",
                        name = "Ivrogne",
                        description = "Performances erratiques au combat",
                        isPositive = false,
                        attackBonus = -0.05f,
                        defenseBonus = -0.05f,
                        moraleBonus = -5f
                    };

                case "old_guard":
                    return new CharacterTrait
                    {
                        traitId = "old_guard",
                        name = "Vieille Garde",
                        description = "Vétéran expérimenté, respecté par les troupes",
                        isPositive = true,
                        defenseBonus = 0.10f,
                        moraleBonus = 10f,
                        movementBonus = -0.5f
                    };

                case "reformer":
                    return new CharacterTrait
                    {
                        traitId = "reformer",
                        name = "Réformateur",
                        description = "Modernise les tactiques militaires",
                        isPositive = true,
                        attackBonus = 0.05f,
                        defenseBonus = 0.05f
                    };

                case "naval_hero":
                    return new CharacterTrait
                    {
                        traitId = "naval_hero",
                        name = "Héros Naval",
                        description = "Excellence dans les combats navals",
                        isPositive = true,
                        attackBonus = 0.10f
                    };

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Skill tree for generals
    /// </summary>
    public static class GeneralSkillTree
    {
        private static Dictionary<string, GeneralSkill> skills;

        static GeneralSkillTree()
        {
            skills = new Dictionary<string, GeneralSkill>();
            InitializeSkills();
        }

        private static void InitializeSkills()
        {
            // === COMMAND BRANCH (Leadership & Morale) ===
            AddSkill(new GeneralSkill("inspiring_presence", "Présence Inspirante",
                "+10 morale des troupes", SkillBranch.Command));
            AddSkill(new GeneralSkill("rally_cry", "Cri de Ralliement",
                "Peut rallier les troupes en déroute", SkillBranch.Command, "inspiring_presence"));
            AddSkill(new GeneralSkill("iron_discipline", "Discipline de Fer",
                "Troupes moins susceptibles de fuir", SkillBranch.Command, "rally_cry"));
            AddSkill(new GeneralSkill("charismatic_leader", "Chef Charismatique",
                "+15 morale, +5% recrutement local", SkillBranch.Command, "inspiring_presence"));
            AddSkill(new GeneralSkill("legendary_commander", "Commandant Légendaire",
                "+20 morale, ennemis -10 morale", SkillBranch.Command, "iron_discipline", "charismatic_leader"));
            AddSkill(new GeneralSkill("imperial_guard", "Garde Impériale",
                "Régiments d'élite sous son commandement", SkillBranch.Command, "legendary_commander"));

            // === TACTICS BRANCH (Combat & Battle) ===
            AddSkill(new GeneralSkill("aggressive_tactics", "Tactiques Agressives",
                "+10% dégâts d'attaque", SkillBranch.Tactics));
            AddSkill(new GeneralSkill("defensive_master", "Maître Défenseur",
                "+10% défense", SkillBranch.Tactics));
            AddSkill(new GeneralSkill("flanking_expert", "Expert en Flanquement",
                "+20% dégâts de flanc", SkillBranch.Tactics, "aggressive_tactics"));
            AddSkill(new GeneralSkill("cavalry_charge", "Charge de Cavalerie",
                "+25% efficacité de charge", SkillBranch.Tactics, "aggressive_tactics"));
            AddSkill(new GeneralSkill("combined_arms", "Armes Combinées",
                "+10% attaque, +10% défense, synergie interarmes", SkillBranch.Tactics, "flanking_expert", "defensive_master"));
            AddSkill(new GeneralSkill("night_attack", "Attaque Nocturne",
                "Peut lancer des attaques de nuit", SkillBranch.Tactics, "flanking_expert"));
            AddSkill(new GeneralSkill("artillery_focus", "Maîtrise de l'Artillerie",
                "+15% dégâts d'artillerie", SkillBranch.Tactics, "combined_arms"));
            AddSkill(new GeneralSkill("envelopment", "Enveloppement",
                "+25% encerclement, ennemi -organisation", SkillBranch.Tactics, "combined_arms"));
            AddSkill(new GeneralSkill("master_strategist", "Maître Stratège",
                "+15% attaque, +10% org, génie tactique total", SkillBranch.Tactics, "envelopment", "artillery_focus"));

            // === LOGISTICS BRANCH (Supply & Movement) ===
            AddSkill(new GeneralSkill("forced_march", "Marche Forcée",
                "+1 point de mouvement", SkillBranch.Logistics));
            AddSkill(new GeneralSkill("supply_master", "Maître du Ravitaillement",
                "-30% attrition de ravitaillement", SkillBranch.Logistics));
            AddSkill(new GeneralSkill("siege_expert", "Expert en Siège",
                "+25% efficacité de siège", SkillBranch.Logistics, "supply_master"));
            AddSkill(new GeneralSkill("forager", "Fourrageur",
                "L'armée vit sur le terrain, attrition réduite", SkillBranch.Logistics, "supply_master"));
            AddSkill(new GeneralSkill("rapid_deployment", "Déploiement Rapide",
                "+2 mouvement, traversée de rivière facilitée", SkillBranch.Logistics, "forced_march"));
            AddSkill(new GeneralSkill("pioneer", "Pionnier",
                "Ponts et routes construits plus vite", SkillBranch.Logistics, "siege_expert"));
            AddSkill(new GeneralSkill("winter_warfare", "Guerre Hivernale",
                "-50% attrition hivernale", SkillBranch.Logistics, "forager"));

            // === INTELLIGENCE BRANCH (Recon & Espionage) ===
            AddSkill(new GeneralSkill("scout_network", "Réseau d'Éclaireurs",
                "+20% reconnaissance, voir les armées ennemies", SkillBranch.Intelligence));
            AddSkill(new GeneralSkill("counter_intelligence", "Contre-Espionnage",
                "L'ennemi ne peut pas voir nos troupes", SkillBranch.Intelligence, "scout_network"));
            AddSkill(new GeneralSkill("deception", "Tromperie",
                "Faux mouvements, confondre l'ennemi", SkillBranch.Intelligence, "scout_network"));
            AddSkill(new GeneralSkill("ambush_master", "Maître de l'Embuscade",
                "+30% dégâts en embuscade", SkillBranch.Intelligence, "deception"));
            AddSkill(new GeneralSkill("spymaster", "Maître Espion",
                "Réseau d'espions dans les provinces ennemies", SkillBranch.Intelligence, "counter_intelligence", "deception"));

            // === ADMINISTRATION BRANCH (Province & Economy) ===
            AddSkill(new GeneralSkill("governor", "Gouverneur",
                "+10% revenus des provinces contrôlées", SkillBranch.Administration));
            AddSkill(new GeneralSkill("tax_collector", "Collecteur d'Impôts",
                "+15% revenus en or des provinces", SkillBranch.Administration, "governor"));
            AddSkill(new GeneralSkill("pacifier", "Pacificateur",
                "-20% mécontentement dans les provinces occupées", SkillBranch.Administration, "governor"));
            AddSkill(new GeneralSkill("recruiter", "Recruteur",
                "+25% recrutement local, régiments plus vite remplis", SkillBranch.Administration, "pacifier"));
            AddSkill(new GeneralSkill("viceroy", "Vice-Roi",
                "Province gouvernée gagne +prospérité, -révolte", SkillBranch.Administration, "tax_collector", "pacifier"));
        }

        private static void AddSkill(GeneralSkill skill)
        {
            skills[skill.skillId] = skill;
        }

        public static GeneralSkill GetSkill(string skillId)
        {
            return skills.TryGetValue(skillId, out var skill) ? skill : null;
        }

        public static List<GeneralSkill> GetSkillsForBranch(SkillBranch branch)
        {
            var result = new List<GeneralSkill>();
            foreach (var skill in skills.Values)
            {
                if (skill.branch == branch)
                    result.Add(skill);
            }
            return result;
        }
    }

    public enum SkillBranch
    {
        Command,
        Tactics,
        Logistics,
        Intelligence,
        Administration
    }

    [System.Serializable]
    public class GeneralSkill
    {
        public string skillId;
        public string name;
        public string description;
        public SkillBranch branch;
        public List<string> prerequisites = new List<string>();

        public GeneralSkill(string id, string name, string desc, SkillBranch branch, params string[] prereqs)
        {
            this.skillId = id;
            this.name = name;
            this.description = desc;
            this.branch = branch;
            this.prerequisites = new List<string>(prereqs);
        }
    }

    /// <summary>
    /// Royal family and succession system
    /// </summary>
    [System.Serializable]
    public class RoyalFamily
    {
        public FactionType faction;
        public string rulerId;
        public string heirId;
        public List<string> familyMemberIds = new List<string>();

        public RoyalFamily(FactionType faction)
        {
            this.faction = faction;
        }

        public void SetRuler(GeneralData ruler)
        {
            rulerId = ruler.generalId;
            ruler.isRuler = true;
            
            if (!familyMemberIds.Contains(ruler.generalId))
                familyMemberIds.Add(ruler.generalId);
        }

        public void SetHeir(GeneralData heir)
        {
            heirId = heir.generalId;
            heir.isHeir = true;
            
            if (!familyMemberIds.Contains(heir.generalId))
                familyMemberIds.Add(heir.generalId);
        }

        public void OnRulerDeath(Dictionary<string, GeneralData> allGenerals)
        {
            if (string.IsNullOrEmpty(heirId))
            {
                // Succession crisis!
                Debug.Log($"[Royal] {faction} has no heir! Succession crisis!");
                return;
            }

            if (allGenerals.TryGetValue(heirId, out GeneralData heir))
            {
                heir.isHeir = false;
                SetRuler(heir);
                
                // Find new heir
                FindNewHeir(allGenerals);
                
                Debug.Log($"[Royal] {heir.FullName} ascends to the throne of {faction}!");
            }
        }

        private void FindNewHeir(Dictionary<string, GeneralData> allGenerals)
        {
            // Find oldest male family member who isn't ruler
            GeneralData bestHeir = null;
            
            foreach (var memberId in familyMemberIds)
            {
                if (memberId == rulerId) continue;
                
                if (allGenerals.TryGetValue(memberId, out GeneralData member))
                {
                    if (!member.isAlive) continue;
                    
                    if (bestHeir == null || member.age > bestHeir.age)
                    {
                        bestHeir = member;
                    }
                }
            }

            if (bestHeir != null)
            {
                SetHeir(bestHeir);
            }
            else
            {
                heirId = null;
            }
        }
    }
}
