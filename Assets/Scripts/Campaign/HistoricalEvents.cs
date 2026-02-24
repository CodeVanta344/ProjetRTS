using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Scripted historical events that trigger based on turn number or conditions.
    /// Adds narrative flavor and strategic challenges to the campaign.
    /// </summary>

    [System.Serializable]
    public class CampaignEvent
    {
        public string id;
        public string title;
        public string description;
        public int triggerTurn;                  // 0 = condition-based only
        public bool triggered;
        public bool requiresChoice;

        // Conditions
        public FactionType targetFaction;
        public System.Func<CampaignManager, bool> condition;

        // Effects
        public System.Action<CampaignManager, int> applyEffect; // int = choice index (0 or 1)

        // Choice options (if requiresChoice)
        public string choiceA;
        public string choiceB;
    }

    public class HistoricalEventSystem
    {
        private List<CampaignEvent> events = new List<CampaignEvent>();
        private Queue<CampaignEvent> pendingEvents = new Queue<CampaignEvent>();

        public CampaignEvent CurrentPendingEvent => pendingEvents.Count > 0 ? pendingEvents.Peek() : null;
        public bool HasPendingEvent => pendingEvents.Count > 0;

        public delegate void EventTriggered(CampaignEvent evt);
        public event EventTriggered OnEventTriggered;

        public HistoricalEventSystem()
        {
            CreateHistoricalEvents();
        }

        private void CreateHistoricalEvents()
        {
            // === 1805: War of the Third Coalition ===
            events.Add(new CampaignEvent
            {
                id = "third_coalition",
                title = "War of the Third Coalition (1805)",
                description = "Austria and Russia form a coalition against France. Austria declares war!",
                triggerTurn = 2,
                targetFaction = FactionType.France,
                applyEffect = (cm, choice) =>
                {
                    cm.Factions[FactionType.Austria].relations[FactionType.France].state = DiplomacyState.War;
                    cm.Factions[FactionType.France].relations[FactionType.Austria].state = DiplomacyState.War;
                    Debug.Log("[Event] Austria declares war on France!");
                }
            });

            // === Turn 4: Continental System ===
            events.Add(new CampaignEvent
            {
                id = "continental_system",
                title = "The Continental System",
                description = "Napoleon proposes a trade blockade against Britain. This will hurt British economy but may anger neutral nations.",
                triggerTurn = 4,
                targetFaction = FactionType.France,
                requiresChoice = true,
                choiceA = "Enforce the blockade (-30% British gold, -10 relations with neutrals)",
                choiceB = "Abandon the plan (no effect)",
                applyEffect = (cm, choice) =>
                {
                    if (choice == 0)
                    {
                        // Blockade: hurt Britain, anger neutrals
                        cm.Factions[FactionType.Britain].gold -= 500f;
                        foreach (var kvp in cm.Factions)
                        {
                            if (kvp.Key != FactionType.France && kvp.Key != FactionType.Britain)
                            {
                                if (kvp.Value.relations.ContainsKey(FactionType.France))
                                    kvp.Value.relations[FactionType.France].relationScore -= 10;
                            }
                        }
                        Debug.Log("[Event] Continental System enforced!");
                    }
                }
            });

            // === Turn 6: Spanish Uprising ===
            events.Add(new CampaignEvent
            {
                id = "spanish_uprising",
                title = "Spanish Uprising",
                description = "The people of Spain revolt against French occupation! Garrison troops are needed.",
                triggerTurn = 6,
                targetFaction = FactionType.France,
                requiresChoice = true,
                choiceA = "Send reinforcements (-300 gold, keep Spain)",
                choiceB = "Abandon Spain (lose Northern Spain province)",
                applyEffect = (cm, choice) =>
                {
                    if (choice == 0)
                    {
                        cm.Factions[FactionType.France].gold -= 300f;
                        Debug.Log("[Event] Reinforcements sent to Spain.");
                    }
                    else
                    {
                        if (cm.Provinces.ContainsKey("spain_north"))
                        {
                            cm.Provinces["spain_north"].owner = FactionType.Britain;
                            cm.Factions[FactionType.France].ownedProvinceIds.Remove("spain_north");
                            cm.Factions[FactionType.Britain].ownedProvinceIds.Add("spain_north");
                        }
                        Debug.Log("[Event] France abandons Northern Spain!");
                    }
                }
            });

            // === Turn 8: Russian Winter ===
            events.Add(new CampaignEvent
            {
                id = "russian_winter",
                title = "The Russian Winter",
                description = "A brutal winter strikes! All armies in Russian territory suffer attrition.",
                triggerTurn = 8,
                targetFaction = FactionType.France,
                applyEffect = (cm, choice) =>
                {
                    string[] russianProvinces = { "moscow", "warsaw", "smolensk" };
                    foreach (var army in cm.Armies.Values)
                    {
                        foreach (string rp in russianProvinces)
                        {
                            if (army.currentProvinceId == rp && army.faction != FactionType.Russia)
                            {
                                // Lose 20% of each regiment
                                foreach (var reg in army.regiments)
                                {
                                    int losses = Mathf.RoundToInt(reg.currentSize * 0.2f);
                                    reg.currentSize = Mathf.Max(reg.currentSize - losses, 1);
                                }
                                Debug.Log($"[Event] {army.armyName} suffers winter attrition!");
                            }
                        }
                    }
                }
            });

            // === Turn 10: Prussian Reform ===
            events.Add(new CampaignEvent
            {
                id = "prussian_reform",
                title = "Prussian Military Reform",
                description = "Prussia modernizes its army after earlier defeats. Prussian units gain +10% combat effectiveness.",
                triggerTurn = 10,
                targetFaction = FactionType.Prussia,
                applyEffect = (cm, choice) =>
                {
                    // Give Prussia free tech
                    var prussiaTech = cm.Factions[FactionType.Prussia].techTree;
                    if (prussiaTech != null && !prussiaTech.IsResearched("military_discipline"))
                    {
                        prussiaTech.StartResearch("military_discipline", cm.Factions[FactionType.Prussia]);
                        // Instant complete
                        for (int i = 0; i < 10; i++) prussiaTech.AdvanceResearch();
                    }
                    Debug.Log("[Event] Prussia reforms its military!");
                }
            });

            // === Condition-based: Player conquers Vienna ===
            events.Add(new CampaignEvent
            {
                id = "treaty_of_pressburg",
                title = "Treaty of Pressburg",
                description = "With Vienna captured, Austria sues for peace. Accept the treaty or continue the war?",
                targetFaction = FactionType.France,
                requiresChoice = true,
                choiceA = "Accept peace (+500 gold, Austria becomes neutral)",
                choiceB = "Refuse and continue the war",
                condition = (cm) =>
                {
                    return cm.Provinces.ContainsKey("vienna") &&
                           cm.Provinces["vienna"].owner == FactionType.France;
                },
                applyEffect = (cm, choice) =>
                {
                    if (choice == 0)
                    {
                        cm.Factions[FactionType.France].gold += 500f;
                        cm.Factions[FactionType.Austria].relations[FactionType.France].state = DiplomacyState.Neutral;
                        cm.Factions[FactionType.France].relations[FactionType.Austria].state = DiplomacyState.Neutral;
                        Debug.Log("[Event] Treaty of Pressburg signed!");
                    }
                }
            });

            // === Condition-based: Player loses Paris ===
            events.Add(new CampaignEvent
            {
                id = "fall_of_paris",
                title = "The Fall of Paris!",
                description = "Paris has fallen to the enemy! The Empire is in grave danger. Rally the nation!",
                targetFaction = FactionType.France,
                applyEffect = (cm, choice) =>
                {
                    foreach (var army in cm.Armies.Values)
                    {
                        if (army.faction == FactionType.France)
                        {
                            army.AddRegiment(new RegimentData("Levée en Masse", UnitType.LineInfantry, 80));
                            Debug.Log("[Event] Emergency conscription! Levée en Masse raised!");
                            break;
                        }
                    }
                },
                condition = (cm) =>
                {
                    return cm.Provinces.ContainsKey("paris") &&
                           cm.Provinces["paris"].owner != FactionType.France;
                }
            });

            // ================================================================
            // EXPANDED EVENTS — FRANCE
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "fr_coronation",
                title = "Couronnement de l'Empereur",
                description = "Napoléon se couronne Empereur des Français à Notre-Dame. Le peuple est divisé.",
                triggerTurn = 3, targetFaction = FactionType.France,
                requiresChoice = true,
                choiceA = "Cérémonie grandiose (+10% war support, -200 or)",
                choiceB = "Cérémonie modeste (+5% stabilité)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) { cm.Factions[FactionType.France].warSupport += 0.10f; cm.Factions[FactionType.France].gold -= 200f; }
                    else cm.Factions[FactionType.France].stability += 0.05f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_marshals",
                title = "Les Maréchaux d'Empire",
                description = "Napoléon nomme ses maréchaux. Cette décision renforcera l'armée.",
                triggerTurn = 5, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].manpower += 5000; }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_austerlitz_glory",
                title = "Souvenir d'Austerlitz",
                description = "La victoire d'Austerlitz résonne dans toute l'Europe. Le moral des troupes est au plus haut.",
                triggerTurn = 7, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].warSupport += 0.05f; }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_code_civil",
                title = "Diffusion du Code Civil",
                description = "Le Code Napoléon se répand dans les territoires conquis, modernisant les lois.",
                triggerTurn = 9, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].stability += 0.03f; cm.Factions[FactionType.France].politicalPower += 20f; }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_trafalgar",
                title = "Bataille de Trafalgar",
                description = "La flotte franco-espagnole est détruite par Nelson. La marine française est anéantie.",
                triggerTurn = 4, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => {
                    cm.Factions[FactionType.France].navalYards = Mathf.Max(0, cm.Factions[FactionType.France].navalYards - 2);
                    cm.Factions[FactionType.Britain].warSupport += 0.15f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_banque",
                title = "Création de la Banque de France",
                description = "Napoléon fonde la Banque de France pour stabiliser l'économie.",
                triggerTurn = 11, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].gold += 400f; cm.Factions[FactionType.France].civilianFactories += 1; }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_lycees",
                title = "Fondation des Lycées",
                description = "Le système éducatif est réformé. Les lycées forment la future élite de la nation.",
                triggerTurn = 13, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].politicalPower += 30f; }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_legion_honneur",
                title = "Légion d'Honneur",
                description = "La Légion d'Honneur récompense le mérite. Le moral des troupes s'améliore.",
                triggerTurn = 15, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].warSupport += 0.03f; cm.Factions[FactionType.France].stability += 0.02f; }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_divorce",
                title = "Divorce de Joséphine",
                description = "L'Empereur divorce de Joséphine pour assurer la succession. Le peuple est ému.",
                triggerTurn = 18, targetFaction = FactionType.France,
                requiresChoice = true,
                choiceA = "Épouser Marie-Louise d'Autriche (+relations Autriche)",
                choiceB = "Rester célibataire (+PP)",
                applyEffect = (cm, choice) => {
                    if (choice == 0 && cm.Factions.ContainsKey(FactionType.Austria))
                        cm.Factions[FactionType.Austria].relations[FactionType.France].relationScore += 20;
                    else cm.Factions[FactionType.France].politicalPower += 50f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "fr_metrique",
                title = "Système Métrique",
                description = "Adoption universelle du système métrique. La standardisation profite au commerce.",
                triggerTurn = 20, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].gold += 150f; }
            });

            // ================================================================
            // EXPANDED EVENTS — BRITAIN
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "gb_nelson_death",
                title = "Mort de Nelson",
                description = "L'amiral Nelson meurt à Trafalgar, mais sa victoire assure la suprématie navale britannique.",
                triggerTurn = 4, targetFaction = FactionType.Britain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Britain].warSupport += 0.10f; }
            });

            events.Add(new CampaignEvent
            {
                id = "gb_act_union",
                title = "Acte d'Union avec l'Irlande",
                description = "L'Irlande est intégrée au Royaume-Uni. Cela stabilise mais crée du mécontentement.",
                triggerTurn = 3, targetFaction = FactionType.Britain,
                requiresChoice = true,
                choiceA = "Intégration stricte (+manpower, -stabilité irlandaise)",
                choiceB = "Autonomie partielle (+stabilité, -contrôle)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) cm.Factions[FactionType.Britain].manpower += 5000;
                    else cm.Factions[FactionType.Britain].stability += 0.05f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "gb_luddites",
                title = "Révolte des Luddites",
                description = "Des ouvriers détruisent les machines dans les manufactures. La révolution industrielle est menacée.",
                triggerTurn = 12, targetFaction = FactionType.Britain,
                requiresChoice = true,
                choiceA = "Réprimer (-100 or, +production)",
                choiceB = "Négocier (+stabilité, -production temporaire)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) cm.Factions[FactionType.Britain].gold -= 100f;
                    else cm.Factions[FactionType.Britain].stability += 0.05f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "gb_east_india",
                title = "Expansion de la Compagnie des Indes",
                description = "La Compagnie des Indes Orientales étend son influence en Asie.",
                triggerTurn = 8, targetFaction = FactionType.Britain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Britain].gold += 500f; }
            });

            events.Add(new CampaignEvent
            {
                id = "gb_abolition",
                title = "Abolition de la traite négrière",
                description = "Le Parlement vote l'abolition de la traite des esclaves.",
                triggerTurn = 6, targetFaction = FactionType.Britain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Britain].stability += 0.05f; cm.Factions[FactionType.Britain].politicalPower += 30f; }
            });

            events.Add(new CampaignEvent
            {
                id = "gb_peninsular",
                title = "Débarquement au Portugal",
                description = "Wellington débarque au Portugal pour aider les Portugais contre les Français.",
                triggerTurn = 10, targetFaction = FactionType.Britain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Britain].warSupport += 0.05f; cm.Factions[FactionType.Britain].manpower += 3000; }
            });

            events.Add(new CampaignEvent
            {
                id = "gb_steam",
                title = "Machine à Vapeur de Watt",
                description = "La machine à vapeur révolutionne l'industrie britannique.",
                triggerTurn = 14, targetFaction = FactionType.Britain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Britain].civilianFactories += 2; }
            });

            // ================================================================
            // EXPANDED EVENTS — PRUSSIA
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "pr_jena",
                title = "Humiliation d'Iéna",
                description = "La Prusse est écrasée à Iéna-Auerstädt. L'armée est en déroute.",
                triggerTurn = 3, targetFaction = FactionType.Prussia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Prussia].warSupport -= 0.10f; cm.Factions[FactionType.Prussia].stability -= 0.10f; }
            });

            events.Add(new CampaignEvent
            {
                id = "pr_tilsit",
                title = "Traité de Tilsit",
                description = "La Prusse est humiliée par le traité de Tilsit. Doit-elle accepter ou résister secrètement?",
                triggerTurn = 5, targetFaction = FactionType.Prussia,
                requiresChoice = true,
                choiceA = "Accepter l'humiliation (+stabilité, perte de provinces)",
                choiceB = "Résistance secrète (+war support, -relations France)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) cm.Factions[FactionType.Prussia].stability += 0.05f;
                    else { cm.Factions[FactionType.Prussia].warSupport += 0.10f; }
                }
            });

            events.Add(new CampaignEvent
            {
                id = "pr_queen_louise",
                title = "La Reine Louise — Symbole National",
                description = "La reine Louise de Prusse devient un symbole de résistance patriotique.",
                triggerTurn = 7, targetFaction = FactionType.Prussia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Prussia].warSupport += 0.05f; cm.Factions[FactionType.Prussia].stability += 0.03f; }
            });

            events.Add(new CampaignEvent
            {
                id = "pr_university_berlin",
                title = "Fondation de l'Université de Berlin",
                description = "Humboldt fonde l'Université de Berlin, centre d'excellence académique.",
                triggerTurn = 12, targetFaction = FactionType.Prussia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Prussia].politicalPower += 30f; }
            });

            events.Add(new CampaignEvent
            {
                id = "pr_freikorps",
                title = "Freikorps — Volontaires patriotes",
                description = "Des volontaires patriotes forment des corps francs contre l'occupation française.",
                triggerTurn = 16, targetFaction = FactionType.Prussia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Prussia].manpower += 8000; cm.Factions[FactionType.Prussia].warSupport += 0.05f; }
            });

            // ================================================================
            // EXPANDED EVENTS — RUSSIA
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "ru_alexander",
                title = "Réformes d'Alexandre Ier",
                description = "Le tsar Alexandre modernise l'administration russe.",
                triggerTurn = 3, targetFaction = FactionType.Russia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Russia].stability += 0.05f; cm.Factions[FactionType.Russia].politicalPower += 20f; }
            });

            events.Add(new CampaignEvent
            {
                id = "ru_kutuzov",
                title = "Nomination de Koutouzov",
                description = "Le vétéran Koutouzov est nommé commandant suprême de l'armée russe.",
                triggerTurn = 9, targetFaction = FactionType.Russia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Russia].warSupport += 0.05f; }
            });

            events.Add(new CampaignEvent
            {
                id = "ru_moscow_fire",
                title = "Incendie de Moscou",
                description = "Moscou brûle plutôt que de tomber aux mains de l'ennemi! Un sacrifice terrible.",
                targetFaction = FactionType.Russia,
                condition = (cm) => cm.Provinces.ContainsKey("moscow") && cm.Provinces["moscow"].owner != FactionType.Russia,
                applyEffect = (cm, choice) => {
                    if (cm.Provinces.ContainsKey("moscow")) cm.Provinces["moscow"].ApplyBattleDevastation(50f);
                    cm.Factions[FactionType.Russia].warSupport += 0.20f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "ru_cossack_raid",
                title = "Raids Cosaques",
                description = "Les Cosaques harcèlent les lignes de ravitaillement ennemies.",
                triggerTurn = 11, targetFaction = FactionType.Russia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Russia].manpower += 3000; }
            });

            events.Add(new CampaignEvent
            {
                id = "ru_decembrist",
                title = "Conspiration Décembriste",
                description = "Des officiers libéraux complotent contre l'absolutisme tsariste.",
                triggerTurn = 22, targetFaction = FactionType.Russia,
                requiresChoice = true,
                choiceA = "Écraser la conspiration (-PP, +stabilité)",
                choiceB = "Tolérer la dissidence (+PP, -stabilité)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) { cm.Factions[FactionType.Russia].politicalPower -= 30f; cm.Factions[FactionType.Russia].stability += 0.05f; }
                    else { cm.Factions[FactionType.Russia].politicalPower += 30f; cm.Factions[FactionType.Russia].stability -= 0.05f; }
                }
            });

            // ================================================================
            // EXPANDED EVENTS — AUSTRIA
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "at_holy_roman",
                title = "Dissolution du Saint-Empire",
                description = "Le Saint-Empire Romain Germanique est dissous. L'Autriche perd son titre impérial.",
                triggerTurn = 5, targetFaction = FactionType.Austria,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Austria].stability -= 0.05f; cm.Factions[FactionType.Austria].politicalPower -= 20f; }
            });

            events.Add(new CampaignEvent
            {
                id = "at_andreas_hofer",
                title = "Révolte d'Andreas Hofer",
                description = "Les Tyroliens se soulèvent contre l'occupation bavaroise et française.",
                triggerTurn = 8, targetFaction = FactionType.Austria,
                requiresChoice = true,
                choiceA = "Soutenir la révolte (+war support, -relations France)",
                choiceB = "Ignorer (+stabilité)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) cm.Factions[FactionType.Austria].warSupport += 0.10f;
                    else cm.Factions[FactionType.Austria].stability += 0.03f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "at_metternich_rise",
                title = "Ascension de Metternich",
                description = "Le prince de Metternich devient chancelier. Sa diplomatie est légendaire.",
                triggerTurn = 12, targetFaction = FactionType.Austria,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Austria].politicalPower += 50f; }
            });

            events.Add(new CampaignEvent
            {
                id = "at_wagram",
                title = "Leçons de Wagram",
                description = "Après Wagram, l'Autriche modernise sa tactique militaire.",
                triggerTurn = 10, targetFaction = FactionType.Austria,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Austria].militaryFactories += 1; cm.Factions[FactionType.Austria].manpower += 3000; }
            });

            // ================================================================
            // EXPANDED EVENTS — SPAIN
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "sp_dos_mayo",
                title = "Dos de Mayo",
                description = "Le peuple de Madrid se soulève contre l'occupation française. La guérilla commence!",
                triggerTurn = 5, targetFaction = FactionType.Spain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Spain].warSupport += 0.15f; cm.Factions[FactionType.Spain].manpower += 5000; }
            });

            events.Add(new CampaignEvent
            {
                id = "sp_cadiz_cortes",
                title = "Cortès de Cadix",
                description = "Les Cortès se réunissent à Cadix pour rédiger une constitution libérale.",
                triggerTurn = 10, targetFaction = FactionType.Spain,
                requiresChoice = true,
                choiceA = "Adopter la constitution (+PP, +stabilité)",
                choiceB = "Rejeter (+war support, autoritarisme)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) { cm.Factions[FactionType.Spain].politicalPower += 40f; cm.Factions[FactionType.Spain].stability += 0.05f; }
                    else cm.Factions[FactionType.Spain].warSupport += 0.05f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "sp_guerrilleros",
                title = "Guérilleros héroïques",
                description = "Les guérilleros espagnols infligent de lourdes pertes aux occupants.",
                triggerTurn = 8, targetFaction = FactionType.Spain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Spain].manpower += 4000; }
            });

            events.Add(new CampaignEvent
            {
                id = "sp_colonial_crisis",
                title = "Crise Coloniale",
                description = "Les colonies d'Amérique latine réclament l'indépendance.",
                triggerTurn = 16, targetFaction = FactionType.Spain,
                requiresChoice = true,
                choiceA = "Envoyer des troupes (-manpower, garder les colonies)",
                choiceB = "Accepter l'indépendance (-or, +stabilité)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) cm.Factions[FactionType.Spain].manpower -= 5000;
                    else { cm.Factions[FactionType.Spain].gold -= 500f; cm.Factions[FactionType.Spain].stability += 0.05f; }
                }
            });

            // ================================================================
            // EXPANDED EVENTS — OTTOMAN
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "ot_selim_reform",
                title = "Réformes de Selim III",
                description = "Le sultan Selim III tente de moderniser l'armée ottomane.",
                triggerTurn = 3, targetFaction = FactionType.Ottoman,
                requiresChoice = true,
                choiceA = "Soutenir les réformes (+usines, -stabilité)",
                choiceB = "Apaiser les conservateurs (+stabilité)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) { cm.Factions[FactionType.Ottoman].militaryFactories += 1; cm.Factions[FactionType.Ottoman].stability -= 0.05f; }
                    else cm.Factions[FactionType.Ottoman].stability += 0.05f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "ot_wahhabist",
                title = "Révolte Wahhabite",
                description = "Les Wahhabites se soulèvent en Arabie, menaçant le contrôle ottoman.",
                triggerTurn = 7, targetFaction = FactionType.Ottoman,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Ottoman].stability -= 0.05f; }
            });

            events.Add(new CampaignEvent
            {
                id = "ot_greek_revolt",
                title = "Insurrection Grecque",
                description = "Les Grecs se soulèvent pour l'indépendance. L'Europe entière observe.",
                triggerTurn = 14, targetFaction = FactionType.Ottoman,
                requiresChoice = true,
                choiceA = "Réprimer brutalement (-diplomatie, +contrôle)",
                choiceB = "Négocier l'autonomie (+stabilité, perte d'influence)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) cm.Factions[FactionType.Ottoman].warSupport += 0.05f;
                    else cm.Factions[FactionType.Ottoman].stability += 0.05f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "ot_janissary_mutiny",
                title = "Mutinerie des Janissaires",
                description = "Les Janissaires refusent les réformes et menacent de renverser le sultan.",
                triggerTurn = 9, targetFaction = FactionType.Ottoman,
                requiresChoice = true,
                choiceA = "Écraser les Janissaires (-manpower, +modernisation)",
                choiceB = "Céder aux Janissaires (+stabilité, -réformes)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) { cm.Factions[FactionType.Ottoman].manpower -= 5000; cm.Factions[FactionType.Ottoman].militaryFactories += 2; }
                    else cm.Factions[FactionType.Ottoman].stability += 0.05f;
                }
            });

            // ================================================================
            // EXPANDED EVENTS — PORTUGAL
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "pt_royal_flight",
                title = "Fuite de la Famille Royale",
                description = "La famille royale portugaise fuit au Brésil devant l'invasion française.",
                triggerTurn = 6, targetFaction = FactionType.Portugal,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Portugal].stability -= 0.05f; cm.Factions[FactionType.Portugal].gold += 300f; }
            });

            events.Add(new CampaignEvent
            {
                id = "pt_british_help",
                title = "Aide britannique au Portugal",
                description = "La Grande-Bretagne envoie des troupes et de l'argent pour défendre le Portugal.",
                triggerTurn = 9, targetFaction = FactionType.Portugal,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Portugal].gold += 400f; cm.Factions[FactionType.Portugal].manpower += 3000; }
            });

            // ================================================================
            // EXPANDED EVENTS — SWEDEN
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "sw_coup",
                title = "Coup d'État de 1809",
                description = "Le roi Gustave IV est renversé. Le maréchal Bernadotte est élu prince héritier.",
                triggerTurn = 8, targetFaction = FactionType.Sweden,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Sweden].stability += 0.05f; cm.Factions[FactionType.Sweden].politicalPower += 30f; }
            });

            events.Add(new CampaignEvent
            {
                id = "sw_finland_loss",
                title = "Perte de la Finlande",
                description = "La Russie annexe la Finlande. Un coup dur pour la Suède.",
                triggerTurn = 10, targetFaction = FactionType.Sweden,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Sweden].warSupport += 0.05f; cm.Factions[FactionType.Sweden].stability -= 0.05f; }
            });

            // ================================================================
            // EXPANDED EVENTS — POLAND
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "pl_hope",
                title = "Espoir Polonais",
                description = "Napoléon promet la restauration de la Pologne. Les patriotes polonais s'enrôlent en masse.",
                triggerTurn = 5, targetFaction = FactionType.Poland,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Poland].manpower += 8000; cm.Factions[FactionType.Poland].warSupport += 0.10f; }
            });

            events.Add(new CampaignEvent
            {
                id = "pl_poniatowski",
                title = "Le Prince Poniatowski",
                description = "Le prince Poniatowski prend le commandement des forces polonaises. Un héros national!",
                triggerTurn = 8, targetFaction = FactionType.Poland,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Poland].warSupport += 0.05f; }
            });

            // ================================================================
            // RANDOM / GLOBAL EVENTS (condition-based)
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "plague_outbreak",
                title = "Épidémie de Peste",
                description = "Une épidémie frappe les provinces du sud. La population est décimée.",
                triggerTurn = 15, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => {
                    foreach (var prov in cm.Provinces.Values)
                        if (prov.mapPosition.y < 0.5f) prov.population = Mathf.Max(100, prov.population - prov.population / 10);
                }
            });

            events.Add(new CampaignEvent
            {
                id = "famine",
                title = "Famine Généralisée",
                description = "Une mauvaise récolte provoque une famine. Les provinces perdent de la prospérité.",
                triggerTurn = 20, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => {
                    foreach (var prov in cm.Provinces.Values)
                        prov.prosperity = Mathf.Max(0f, prov.prosperity - 10f);
                }
            });

            events.Add(new CampaignEvent
            {
                id = "volcanic_winter",
                title = "Année sans Été (Tambora)",
                description = "L'éruption du Tambora provoque un hiver volcanique. La production agricole chute.",
                triggerTurn = 25, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => {
                    foreach (var fkvp in cm.Factions)
                        fkvp.Value.gold -= 100f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "trade_boom",
                title = "Boom Commercial",
                description = "Un afflux de marchandises exotiques enrichit les ports européens.",
                triggerTurn = 17, targetFaction = FactionType.Britain,
                applyEffect = (cm, choice) => {
                    foreach (var fkvp in cm.Factions)
                        if (fkvp.Value.ownedProvinceIds.Count > 3) fkvp.Value.gold += 200f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "desertion_crisis",
                title = "Crise de Désertion",
                description = "La lassitude de la guerre provoque des désertions massives dans toutes les armées.",
                triggerTurn = 24, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => {
                    foreach (var army in cm.Armies.Values)
                        foreach (var reg in army.regiments)
                            reg.currentSize = Mathf.Max(10, reg.currentSize - 5);
                }
            });

            events.Add(new CampaignEvent
            {
                id = "great_comet",
                title = "La Grande Comète de 1811",
                description = "Une comète brillante traverse le ciel. Les vignobles produisent un millésime exceptionnel!",
                triggerTurn = 19, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => {
                    foreach (var fkvp in cm.Factions)
                        fkvp.Value.stability = Mathf.Clamp01(fkvp.Value.stability + 0.02f);
                }
            });

            events.Add(new CampaignEvent
            {
                id = "diplomatic_incident",
                title = "Incident Diplomatique",
                description = "Un ambassadeur est insulté lors d'un banquet. Les relations se tendent.",
                triggerTurn = 13, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].politicalPower -= 10f; }
            });

            events.Add(new CampaignEvent
            {
                id = "scientific_discovery",
                title = "Découverte Scientifique",
                description = "Un savant fait une découverte révolutionnaire en chimie. La recherche progresse.",
                triggerTurn = 21, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].politicalPower += 20f; }
            });

            // ================================================================
            // CONDITION-BASED EVENTS (universal)
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "fall_of_berlin",
                title = "Chute de Berlin!",
                description = "Berlin est tombée! La Prusse tremble.",
                targetFaction = FactionType.Prussia,
                condition = (cm) => cm.Provinces.ContainsKey("berlin") && cm.Provinces["berlin"].owner != FactionType.Prussia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Prussia].stability -= 0.15f; cm.Factions[FactionType.Prussia].warSupport += 0.10f; }
            });

            events.Add(new CampaignEvent
            {
                id = "fall_of_vienna",
                title = "Chute de Vienne!",
                description = "Vienne est aux mains de l'ennemi. L'Empire d'Autriche vacille.",
                targetFaction = FactionType.Austria,
                condition = (cm) => cm.Provinces.ContainsKey("vienna") && cm.Provinces["vienna"].owner != FactionType.Austria,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Austria].stability -= 0.15f; }
            });

            events.Add(new CampaignEvent
            {
                id = "fall_of_moscow",
                title = "Chute de Moscou!",
                description = "Moscou est prise! Mais la Russie ne capitule jamais.",
                targetFaction = FactionType.Russia,
                condition = (cm) => cm.Provinces.ContainsKey("moscow") && cm.Provinces["moscow"].owner != FactionType.Russia,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Russia].warSupport += 0.20f; cm.Factions[FactionType.Russia].manpower += 10000; }
            });

            events.Add(new CampaignEvent
            {
                id = "fall_of_london",
                title = "Invasion de Londres!",
                description = "L'impensable s'est produit — Londres est menacée! La nation se mobilise.",
                targetFaction = FactionType.Britain,
                condition = (cm) => cm.Provinces.ContainsKey("london") && cm.Provinces["london"].owner != FactionType.Britain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Britain].warSupport += 0.25f; cm.Factions[FactionType.Britain].manpower += 15000; }
            });

            events.Add(new CampaignEvent
            {
                id = "fall_of_constantinople",
                title = "Constantinople menacée!",
                description = "Constantinople est attaquée! Le cœur de l'Empire est en danger.",
                targetFaction = FactionType.Ottoman,
                condition = (cm) => cm.Provinces.ContainsKey("constantinople") && cm.Provinces["constantinople"].owner != FactionType.Ottoman,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Ottoman].warSupport += 0.20f; cm.Factions[FactionType.Ottoman].manpower += 8000; }
            });

            events.Add(new CampaignEvent
            {
                id = "fall_of_madrid",
                title = "Chute de Madrid!",
                description = "Madrid tombe. Le peuple espagnol intensifie sa résistance.",
                targetFaction = FactionType.Spain,
                condition = (cm) => cm.Provinces.ContainsKey("madrid") && cm.Provinces["madrid"].owner != FactionType.Spain,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.Spain].warSupport += 0.15f; cm.Factions[FactionType.Spain].manpower += 8000; }
            });

            // ================================================================
            // LATE GAME EVENTS
            // ================================================================

            events.Add(new CampaignEvent
            {
                id = "congress_vienna",
                title = "Congrès de Vienne",
                description = "Les puissances européennes se réunissent pour redessiner la carte de l'Europe.",
                triggerTurn = 30, targetFaction = FactionType.France,
                requiresChoice = true,
                choiceA = "Participer aux négociations (+stabilité, +PP)",
                choiceB = "Refuser (+war support, -diplomatie)",
                applyEffect = (cm, choice) => {
                    if (choice == 0) { cm.Factions[FactionType.France].stability += 0.10f; cm.Factions[FactionType.France].politicalPower += 50f; }
                    else cm.Factions[FactionType.France].warSupport += 0.10f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "hundred_days",
                title = "Les Cent-Jours",
                description = "Napoléon revient! L'Europe tremble à nouveau.",
                triggerTurn = 35, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => { cm.Factions[FactionType.France].warSupport += 0.15f; cm.Factions[FactionType.France].manpower += 10000; }
            });

            events.Add(new CampaignEvent
            {
                id = "romantic_nationalism",
                title = "Nationalisme Romantique",
                description = "Le mouvement romantique embrase les peuples européens. Les nations s'éveillent.",
                triggerTurn = 28, targetFaction = FactionType.France,
                applyEffect = (cm, choice) => {
                    foreach (var fkvp in cm.Factions)
                        fkvp.Value.warSupport = Mathf.Clamp01(fkvp.Value.warSupport + 0.03f);
                }
            });

            events.Add(new CampaignEvent
            {
                id = "holy_alliance",
                title = "Sainte-Alliance",
                description = "La Russie, l'Autriche et la Prusse forment une alliance conservatrice.",
                triggerTurn = 32, targetFaction = FactionType.Russia,
                applyEffect = (cm, choice) => {
                    if (cm.Factions.ContainsKey(FactionType.Russia)) cm.Factions[FactionType.Russia].stability += 0.05f;
                    if (cm.Factions.ContainsKey(FactionType.Austria)) cm.Factions[FactionType.Austria].stability += 0.05f;
                    if (cm.Factions.ContainsKey(FactionType.Prussia)) cm.Factions[FactionType.Prussia].stability += 0.05f;
                }
            });

            events.Add(new CampaignEvent
            {
                id = "industrial_age",
                title = "Début de l'Ère Industrielle",
                description = "Les machines à vapeur et les manufactures transforment l'Europe.",
                triggerTurn = 40, targetFaction = FactionType.Britain,
                applyEffect = (cm, choice) => {
                    foreach (var fkvp in cm.Factions)
                        fkvp.Value.civilianFactories += 1;
                }
            });

            Debug.Log($"[HistoricalEventSystem] Initialized {events.Count} events");
        }

        /// <summary>
        /// Check and trigger events for the current turn.
        /// Call at the start of each turn.
        /// </summary>
        public void CheckEvents(CampaignManager cm, int currentTurn)
        {
            foreach (var evt in events)
            {
                if (evt.triggered) continue;

                bool shouldTrigger = false;

                // Turn-based trigger
                if (evt.triggerTurn > 0 && currentTurn >= evt.triggerTurn)
                    shouldTrigger = true;

                // Condition-based trigger
                if (evt.condition != null && evt.condition(cm))
                    shouldTrigger = true;

                if (shouldTrigger)
                {
                    evt.triggered = true;
                    pendingEvents.Enqueue(evt);
                    OnEventTriggered?.Invoke(evt);
                    Debug.Log($"[Event] {evt.title}");
                }
            }
        }

        /// <summary>
        /// Resolve the current pending event with a choice (0 or 1).
        /// For non-choice events, pass 0.
        /// </summary>
        public void ResolveCurrentEvent(CampaignManager cm, int choice = 0)
        {
            if (pendingEvents.Count == 0) return;

            CampaignEvent evt = pendingEvents.Dequeue();
            evt.applyEffect?.Invoke(cm, choice);
        }
    }
}
