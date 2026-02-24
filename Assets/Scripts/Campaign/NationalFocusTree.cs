using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// National Focus Trees — per-faction decision trees that grant unique bonuses.
    /// Each focus costs time and sometimes PP. Only one focus can be active at a time.
    /// </summary>

    [System.Serializable]
    public class NationalFocus
    {
        public string focusId;
        public string displayName;
        public string description;
        public int turnsToComplete = 10;
        public float ppCost = 0f;
        public string[] prerequisites = new string[0];
        public string[] mutuallyExclusive = new string[0]; // Can't take if these are completed
        public bool isCompleted = false;

        // Rewards
        public float goldReward;
        public float manpowerReward;
        public float stabilityChange;
        public float warSupportChange;
        public float ppReward;
        public string unlockBuilding;
        public string unlockTech;
        public int civilianFactoryReward;
        public int militaryFactoryReward;
        public int navalYardReward;
        public string claimProvince;     // Province claim for war justification
        public float researchSpeedBonus; // Permanent research speed bonus
        public float productionBonus;    // Permanent production bonus

        public NationalFocus(string id, string name, string desc, int turns)
        {
            focusId = id;
            displayName = name;
            description = desc;
            turnsToComplete = turns;
        }
    }

    public static class NationalFocusTree
    {
        private static Dictionary<FactionType, List<NationalFocus>> focusTrees =
            new Dictionary<FactionType, List<NationalFocus>>();

        // Current active focus per faction
        private static Dictionary<FactionType, string> activeFocus =
            new Dictionary<FactionType, string>();
        private static Dictionary<FactionType, int> focusProgress =
            new Dictionary<FactionType, int>();

        static NationalFocusTree()
        {
            InitializeFocusTrees();
        }

        private static void InitializeFocusTrees()
        {
            // === FRANCE (12 focuses) ===
            focusTrees[FactionType.France] = new List<NationalFocus>
            {
                // Tier 1 — Starting focuses
                new NationalFocus("fr_code", "Code Napoléon", "Réforme juridique unifiant la France", 8)
                    { stabilityChange = 0.05f, civilianFactoryReward = 2, ppReward = 50f },
                new NationalFocus("fr_levee", "Levée en Masse", "Conscription nationale massive", 6)
                    { manpowerReward = 15000, warSupportChange = 0.05f },
                // Tier 2 — Branches
                new NationalFocus("fr_continental", "Système Continental", "Blocus économique contre la Grande-Bretagne", 10)
                    { goldReward = 500f, warSupportChange = 0.10f, prerequisites = new[] { "fr_code" } },
                new NationalFocus("fr_grande_armee", "Grande Armée", "Réorganisation de l'armée impériale", 10)
                    { militaryFactoryReward = 3, manpowerReward = 10000, prerequisites = new[] { "fr_levee" } },
                new NationalFocus("fr_ecole_poly", "École Polytechnique", "Centre d'excellence scientifique et militaire", 8)
                    { researchSpeedBonus = 0.10f, prerequisites = new[] { "fr_code" } },
                // Tier 3 — Choice branch (military vs diplomatic)
                new NationalFocus("fr_imperator", "Imperator", "Couronnement impérial — gloire militaire", 12)
                    { warSupportChange = 0.15f, stabilityChange = 0.10f, militaryFactoryReward = 2,
                      prerequisites = new[] { "fr_grande_armee" }, mutuallyExclusive = new[] { "fr_concordat" } },
                new NationalFocus("fr_concordat", "Concordat", "Paix avec le Pape — stabilité intérieure", 10)
                    { stabilityChange = 0.15f, ppReward = 100f, civilianFactoryReward = 2,
                      prerequisites = new[] { "fr_code" }, mutuallyExclusive = new[] { "fr_imperator" } },
                // Tier 3 continued
                new NationalFocus("fr_egypt", "Campagne d'Égypte", "Expédition vers l'Orient — prestige et science", 10)
                    { goldReward = 400f, researchSpeedBonus = 0.05f, claimProvince = "egypt",
                      prerequisites = new[] { "fr_grande_armee" } },
                new NationalFocus("fr_confederation", "Confédération du Rhin", "États satellites en Allemagne", 10)
                    { goldReward = 300f, manpowerReward = 8000, claimProvince = "bavaria",
                      prerequisites = new[] { "fr_continental" } },
                // Tier 4 — Capstone
                new NationalFocus("fr_domination", "Domination Continentale", "Maître de l'Europe", 15)
                    { goldReward = 1000f, militaryFactoryReward = 3, civilianFactoryReward = 3, manpowerReward = 20000,
                      prerequisites = new[] { "fr_imperator", "fr_confederation" } },
                new NationalFocus("fr_legion", "Légion d'Honneur", "Système de mérite — fidélité et excellence", 6)
                    { stabilityChange = 0.05f, ppReward = 75f, prerequisites = new[] { "fr_ecole_poly" } },
                new NationalFocus("fr_marine", "Marine Impériale", "Reconstruction de la flotte française", 10)
                    { navalYardReward = 3, goldReward = 300f, prerequisites = new[] { "fr_continental" } },
            };

            // === BRITAIN (12 focuses) ===
            focusTrees[FactionType.Britain] = new List<NationalFocus>
            {
                new NationalFocus("gb_naval", "Naval Supremacy", "Domination des mers", 8)
                    { navalYardReward = 3, goldReward = 500f },
                new NationalFocus("gb_coalition", "Coalition Builder", "Former des coalitions anti-françaises", 8)
                    { stabilityChange = 0.10f, warSupportChange = 0.05f, ppReward = 50f },
                new NationalFocus("gb_trafalgar", "Esprit de Trafalgar", "Victoire navale décisive", 10)
                    { navalYardReward = 2, warSupportChange = 0.15f, prerequisites = new[] { "gb_naval" } },
                new NationalFocus("gb_industry", "Industrial Revolution", "Manufactures et mécanisation", 12)
                    { civilianFactoryReward = 4, militaryFactoryReward = 2, productionBonus = 0.10f,
                      prerequisites = new[] { "gb_naval" } },
                new NationalFocus("gb_subsidies", "Continental Subsidies", "Financer les alliés européens", 8)
                    { goldReward = -500f, ppReward = 100f, prerequisites = new[] { "gb_coalition" } },
                new NationalFocus("gb_peninsula", "Peninsular War", "Expédition en Espagne et Portugal", 10)
                    { manpowerReward = 8000, warSupportChange = 0.10f, militaryFactoryReward = 2,
                      prerequisites = new[] { "gb_coalition" }, mutuallyExclusive = new[] { "gb_diplomacy" } },
                new NationalFocus("gb_diplomacy", "Pax Diplomatica", "Diplomatie plutôt que guerre terrestre", 8)
                    { stabilityChange = 0.10f, ppReward = 150f, civilianFactoryReward = 2,
                      prerequisites = new[] { "gb_coalition" }, mutuallyExclusive = new[] { "gb_peninsula" } },
                new NationalFocus("gb_colonies", "Colonial Empire", "Exploitation des colonies mondiales", 10)
                    { goldReward = 800f, civilianFactoryReward = 2, prerequisites = new[] { "gb_trafalgar" } },
                new NationalFocus("gb_blockade", "Continental Blockade", "Blocus naval de l'Europe", 8)
                    { goldReward = 400f, prerequisites = new[] { "gb_trafalgar" } },
                new NationalFocus("gb_reform", "Parliamentary Reform", "Réformes parlementaires modernes", 10)
                    { stabilityChange = 0.10f, ppReward = 80f, researchSpeedBonus = 0.05f,
                      prerequisites = new[] { "gb_industry" } },
                new NationalFocus("gb_empire", "Pax Britannica", "L'Empire sur lequel le soleil ne se couche jamais", 15)
                    { goldReward = 1500f, stabilityChange = 0.15f, navalYardReward = 2,
                      prerequisites = new[] { "gb_colonies", "gb_blockade" } },
                new NationalFocus("gb_wellington", "Wellington's Army", "Armée professionnelle d'élite", 10)
                    { militaryFactoryReward = 3, manpowerReward = 5000, prerequisites = new[] { "gb_peninsula" } },
            };

            // === PRUSSIA (10 focuses) ===
            focusTrees[FactionType.Prussia] = new List<NationalFocus>
            {
                new NationalFocus("pr_reform", "Réformes de Stein", "Modernisation administrative", 8)
                    { stabilityChange = 0.05f, civilianFactoryReward = 2, ppReward = 40f },
                new NationalFocus("pr_scharnhorst", "Réformes Scharnhorst", "Refonte totale de l'armée", 8)
                    { militaryFactoryReward = 2, researchSpeedBonus = 0.05f },
                new NationalFocus("pr_landwehr", "Landwehr", "Milice populaire armée", 10)
                    { manpowerReward = 20000, prerequisites = new[] { "pr_scharnhorst" } },
                new NationalFocus("pr_military", "Kriegsakademie", "Académie de guerre prussienne", 10)
                    { militaryFactoryReward = 3, warSupportChange = 0.10f, researchSpeedBonus = 0.05f,
                      prerequisites = new[] { "pr_scharnhorst" } },
                new NationalFocus("pr_tugendbund", "Tugendbund", "Société secrète patriotique", 6)
                    { warSupportChange = 0.10f, ppReward = 50f, prerequisites = new[] { "pr_reform" } },
                new NationalFocus("pr_industry", "Industrialisation prussienne", "Développement industriel", 10)
                    { civilianFactoryReward = 3, productionBonus = 0.10f, prerequisites = new[] { "pr_reform" } },
                new NationalFocus("pr_befreiung", "Guerre de Libération", "Appel à la nation contre Napoléon", 10)
                    { warSupportChange = 0.20f, manpowerReward = 15000,
                      prerequisites = new[] { "pr_tugendbund", "pr_landwehr" },
                      mutuallyExclusive = new[] { "pr_french_ally" } },
                new NationalFocus("pr_french_ally", "Allié de la France", "Soumission pragmatique à Napoléon", 8)
                    { stabilityChange = 0.10f, goldReward = 500f,
                      prerequisites = new[] { "pr_reform" },
                      mutuallyExclusive = new[] { "pr_befreiung" } },
                new NationalFocus("pr_unification", "Confédération allemande", "Unification des États allemands", 15)
                    { stabilityChange = 0.15f, manpowerReward = 25000, civilianFactoryReward = 3,
                      claimProvince = "saxony",
                      prerequisites = new[] { "pr_befreiung", "pr_military" } },
                new NationalFocus("pr_iron", "Fer et Sang", "Puissance militaire inégalée", 12)
                    { militaryFactoryReward = 4, manpowerReward = 10000,
                      prerequisites = new[] { "pr_industry", "pr_military" } },
            };

            // === RUSSIA (10 focuses) ===
            focusTrees[FactionType.Russia] = new List<NationalFocus>
            {
                new NationalFocus("ru_conscript", "Conscription massive", "L'immense réservoir humain russe", 6)
                    { manpowerReward = 25000 },
                new NationalFocus("ru_speransky", "Réformes Speransky", "Modernisation administrative", 8)
                    { stabilityChange = 0.05f, civilianFactoryReward = 2, ppReward = 50f },
                new NationalFocus("ru_winter", "Guerre Hivernale", "L'hiver est notre allié le plus fidèle", 10)
                    { warSupportChange = 0.10f, manpowerReward = 10000, prerequisites = new[] { "ru_conscript" } },
                new NationalFocus("ru_cossacks", "Cosaques du Don", "Cavalerie légère d'élite", 8)
                    { militaryFactoryReward = 2, manpowerReward = 8000, prerequisites = new[] { "ru_conscript" } },
                new NationalFocus("ru_industry", "Industrialisation", "Manufactures de l'Oural", 12)
                    { militaryFactoryReward = 3, civilianFactoryReward = 3, productionBonus = 0.08f,
                      prerequisites = new[] { "ru_speransky" } },
                new NationalFocus("ru_orthodox", "Foi Orthodoxe", "L'Église soutient le Tsar", 6)
                    { stabilityChange = 0.08f, ppReward = 40f, prerequisites = new[] { "ru_speransky" } },
                new NationalFocus("ru_caucasus", "Conquête du Caucase", "Expansion vers le sud", 10)
                    { manpowerReward = 5000, goldReward = 300f, claimProvince = "georgia",
                      prerequisites = new[] { "ru_cossacks" } },
                new NationalFocus("ru_alliance", "Alliance avec Napoléon", "Tilsit — paix avec la France", 8)
                    { stabilityChange = 0.10f, goldReward = 400f,
                      prerequisites = new[] { "ru_speransky" },
                      mutuallyExclusive = new[] { "ru_patriotic" } },
                new NationalFocus("ru_patriotic", "Guerre Patriotique", "Résistance totale à l'envahisseur", 10)
                    { warSupportChange = 0.20f, manpowerReward = 30000,
                      prerequisites = new[] { "ru_winter" },
                      mutuallyExclusive = new[] { "ru_alliance" } },
                new NationalFocus("ru_bear", "L'Ours Russe", "Puissance impériale incontestée", 15)
                    { manpowerReward = 30000, stabilityChange = 0.10f, militaryFactoryReward = 3,
                      prerequisites = new[] { "ru_patriotic", "ru_industry" } },
            };

            // === AUSTRIA (10 focuses) ===
            focusTrees[FactionType.Austria] = new List<NationalFocus>
            {
                new NationalFocus("at_habsburg", "Héritage Habsbourg", "Traditions impériales séculaires", 8)
                    { stabilityChange = 0.05f, goldReward = 500f, ppReward = 40f },
                new NationalFocus("at_charles", "Réformes de l'Archiduc Charles", "Modernisation militaire", 8)
                    { militaryFactoryReward = 2, manpowerReward = 8000 },
                new NationalFocus("at_metternich", "Système Metternich", "Diplomatie conservatrice", 10)
                    { stabilityChange = 0.10f, ppReward = 100f, prerequisites = new[] { "at_habsburg" } },
                new NationalFocus("at_italian", "Domination italienne", "Contrôle de l'Italie du Nord", 10)
                    { goldReward = 400f, claimProvince = "lombardy",
                      prerequisites = new[] { "at_charles" },
                      mutuallyExclusive = new[] { "at_german" } },
                new NationalFocus("at_german", "Hégémonie allemande", "Influence sur les États allemands", 10)
                    { manpowerReward = 10000, claimProvince = "bavaria",
                      prerequisites = new[] { "at_charles" },
                      mutuallyExclusive = new[] { "at_italian" } },
                new NationalFocus("at_empire", "Empire d'Autriche", "Proclamation de l'empire", 12)
                    { stabilityChange = 0.10f, warSupportChange = 0.10f, civilianFactoryReward = 2,
                      prerequisites = new[] { "at_metternich" } },
                new NationalFocus("at_bohemia", "Forteresse Bohême", "Fortifier les défenses du nord", 8)
                    { militaryFactoryReward = 2, manpowerReward = 5000, prerequisites = new[] { "at_charles" } },
                new NationalFocus("at_hungary", "Compromis hongrois", "Apaiser les Magyars", 8)
                    { stabilityChange = 0.08f, manpowerReward = 12000, prerequisites = new[] { "at_habsburg" } },
                new NationalFocus("at_coalition", "Chef de Coalition", "Mener la coalition anti-française", 10)
                    { warSupportChange = 0.15f, ppReward = 80f,
                      prerequisites = new[] { "at_empire" } },
                new NationalFocus("at_order", "Concert européen", "Équilibre des puissances", 15)
                    { stabilityChange = 0.15f, goldReward = 800f, ppReward = 100f,
                      prerequisites = new[] { "at_coalition", "at_empire" } },
            };

            // === SPAIN (8 focuses) ===
            focusTrees[FactionType.Spain] = new List<NationalFocus>
            {
                new NationalFocus("sp_guerrilla", "Guerre de Guérilla", "Résistance populaire acharnée", 8)
                    { manpowerReward = 12000, warSupportChange = 0.10f },
                new NationalFocus("sp_bourbon", "Restauration Bourbon", "Légitimité royale", 6)
                    { stabilityChange = 0.08f, ppReward = 50f },
                new NationalFocus("sp_colonies", "Empire Colonial", "Richesses du Nouveau Monde", 10)
                    { goldReward = 1000f, civilianFactoryReward = 2, prerequisites = new[] { "sp_bourbon" } },
                new NationalFocus("sp_inquisition", "Sainte Inquisition", "Contrôle religieux strict", 6)
                    { stabilityChange = 0.05f, prerequisites = new[] { "sp_bourbon" },
                      mutuallyExclusive = new[] { "sp_liberal" } },
                new NationalFocus("sp_liberal", "Constitution de Cadix", "Réformes libérales", 8)
                    { ppReward = 80f, researchSpeedBonus = 0.05f, prerequisites = new[] { "sp_bourbon" },
                      mutuallyExclusive = new[] { "sp_inquisition" } },
                new NationalFocus("sp_tercio", "Nouveaux Tercios", "Réforme de l'infanterie espagnole", 10)
                    { militaryFactoryReward = 3, manpowerReward = 8000, prerequisites = new[] { "sp_guerrilla" } },
                new NationalFocus("sp_armada", "Nueva Armada", "Reconstruction de la flotte espagnole", 10)
                    { navalYardReward = 2, goldReward = 300f, prerequisites = new[] { "sp_colonies" } },
                new NationalFocus("sp_reconquista", "Reconquista", "Reprendre les terres perdues", 12)
                    { warSupportChange = 0.15f, manpowerReward = 15000,
                      prerequisites = new[] { "sp_tercio", "sp_guerrilla" } },
            };

            // === OTTOMAN (8 focuses) ===
            focusTrees[FactionType.Ottoman] = new List<NationalFocus>
            {
                new NationalFocus("ot_nizam", "Nizam-ı Cedid", "Nouvelle armée à l'européenne", 8)
                    { militaryFactoryReward = 2, manpowerReward = 12000 },
                new NationalFocus("ot_sultan", "Autorité du Sultan", "Centralisation du pouvoir", 6)
                    { stabilityChange = 0.08f, ppReward = 60f },
                new NationalFocus("ot_janissary", "Abolir les Janissaires", "Supprimer la vieille garde corrompue", 10)
                    { stabilityChange = -0.05f, warSupportChange = 0.10f, militaryFactoryReward = 3,
                      prerequisites = new[] { "ot_nizam" } },
                new NationalFocus("ot_trade", "Routes de la Soie", "Contrôle des routes commerciales", 8)
                    { goldReward = 600f, civilianFactoryReward = 2, prerequisites = new[] { "ot_sultan" } },
                new NationalFocus("ot_egypt", "Reconquête de l'Égypte", "Soumettre les Mamelouks rebelles", 10)
                    { manpowerReward = 8000, goldReward = 400f, claimProvince = "egypt",
                      prerequisites = new[] { "ot_nizam" } },
                new NationalFocus("ot_balkans", "Pacifier les Balkans", "Mater les révoltes balkaniques", 10)
                    { stabilityChange = 0.05f, manpowerReward = 10000,
                      prerequisites = new[] { "ot_janissary" } },
                new NationalFocus("ot_tanzimat", "Tanzimat", "Grandes réformes de modernisation", 12)
                    { civilianFactoryReward = 3, researchSpeedBonus = 0.08f, ppReward = 80f,
                      prerequisites = new[] { "ot_sultan", "ot_trade" } },
                new NationalFocus("ot_caliphate", "Califat Universel", "Leadership du monde musulman", 15)
                    { warSupportChange = 0.15f, manpowerReward = 20000, stabilityChange = 0.10f,
                      prerequisites = new[] { "ot_tanzimat", "ot_balkans" } },
            };

            // === PORTUGAL (6 focuses) ===
            focusTrees[FactionType.Portugal] = new List<NationalFocus>
            {
                new NationalFocus("pt_alliance", "Alliance anglaise", "Plus vieille alliance du monde", 6)
                    { goldReward = 300f, stabilityChange = 0.05f },
                new NationalFocus("pt_brasil", "Or du Brésil", "Richesses coloniales", 8)
                    { goldReward = 800f, civilianFactoryReward = 2 },
                new NationalFocus("pt_lines", "Lignes de Torres Vedras", "Fortifications défensives imprenables", 10)
                    { militaryFactoryReward = 2, prerequisites = new[] { "pt_alliance" } },
                new NationalFocus("pt_reform", "Réformes Pombalines", "Modernisation de l'État", 8)
                    { researchSpeedBonus = 0.05f, ppReward = 50f, prerequisites = new[] { "pt_brasil" } },
                new NationalFocus("pt_navy", "Marine Portugaise", "Tradition navale séculaire", 8)
                    { navalYardReward = 2, prerequisites = new[] { "pt_alliance" } },
                new NationalFocus("pt_resist", "Résistance Portugaise", "Défense de la patrie", 10)
                    { warSupportChange = 0.15f, manpowerReward = 8000,
                      prerequisites = new[] { "pt_lines" } },
            };

            // === SWEDEN (6 focuses) ===
            focusTrees[FactionType.Sweden] = new List<NationalFocus>
            {
                new NationalFocus("sw_bernadotte", "Bernadotte", "Prince héritier français pour la Suède", 6)
                    { stabilityChange = 0.05f, ppReward = 50f },
                new NationalFocus("sw_baltic", "Dominium Maris Baltici", "Contrôle de la Baltique", 8)
                    { navalYardReward = 2, goldReward = 300f },
                new NationalFocus("sw_finland", "Défense de Finlande", "Protéger le flanc est", 8)
                    { militaryFactoryReward = 2, manpowerReward = 5000, prerequisites = new[] { "sw_bernadotte" } },
                new NationalFocus("sw_norway", "Union avec la Norvège", "Annexer la Norvège", 10)
                    { manpowerReward = 8000, goldReward = 200f, claimProvince = "norway",
                      prerequisites = new[] { "sw_bernadotte" } },
                new NationalFocus("sw_iron", "Fer suédois", "Mines de fer de qualité supérieure", 8)
                    { civilianFactoryReward = 2, productionBonus = 0.08f, prerequisites = new[] { "sw_baltic" } },
                new NationalFocus("sw_power", "Grande Puissance du Nord", "Restaurer la grandeur suédoise", 12)
                    { militaryFactoryReward = 2, stabilityChange = 0.10f,
                      prerequisites = new[] { "sw_norway", "sw_iron" } },
            };

            // === DENMARK (4 focuses) ===
            focusTrees[FactionType.Denmark] = new List<NationalFocus>
            {
                new NationalFocus("dk_neutral", "Neutralité armée", "Défendre la neutralité danoise", 6)
                    { stabilityChange = 0.05f, navalYardReward = 1 },
                new NationalFocus("dk_sound", "Péage du Sund", "Contrôle du détroit danois", 8)
                    { goldReward = 500f, prerequisites = new[] { "dk_neutral" } },
                new NationalFocus("dk_norway_union", "Kalmarunion", "Renforcer l'union avec la Norvège", 8)
                    { manpowerReward = 5000, stabilityChange = 0.05f, prerequisites = new[] { "dk_neutral" } },
                new NationalFocus("dk_fleet", "Flotte de la Baltique", "Marine de guerre", 10)
                    { navalYardReward = 2, prerequisites = new[] { "dk_sound" } },
            };

            // === POLAND (6 focuses) ===
            focusTrees[FactionType.Poland] = new List<NationalFocus>
            {
                new NationalFocus("pl_legions", "Légions Polonaises", "Combattre pour la liberté de la Pologne", 6)
                    { manpowerReward = 10000, warSupportChange = 0.10f },
                new NationalFocus("pl_duchy", "Duché de Varsovie", "État polonais restauré", 8)
                    { stabilityChange = 0.10f, civilianFactoryReward = 2, ppReward = 50f },
                new NationalFocus("pl_constitution", "Constitution du 3 mai", "Réformes constitutionnelles", 8)
                    { ppReward = 80f, researchSpeedBonus = 0.05f, prerequisites = new[] { "pl_duchy" } },
                new NationalFocus("pl_uhlans", "Uhlans Polonais", "Cavalerie légendaire", 8)
                    { militaryFactoryReward = 2, manpowerReward = 5000, prerequisites = new[] { "pl_legions" } },
                new NationalFocus("pl_partitions", "Effacer les Partitions", "Reconquérir les terres perdues", 12)
                    { warSupportChange = 0.15f, claimProvince = "galicia",
                      prerequisites = new[] { "pl_constitution", "pl_uhlans" } },
                new NationalFocus("pl_free", "Pologne Libre", "Indépendance totale", 15)
                    { stabilityChange = 0.15f, manpowerReward = 15000,
                      prerequisites = new[] { "pl_partitions" } },
            };

            // === DUTCH (4 focuses) ===
            focusTrees[FactionType.Dutch] = new List<NationalFocus>
            {
                new NationalFocus("nl_trade", "Compagnie des Indes", "Empire commercial mondial", 8)
                    { goldReward = 600f, navalYardReward = 1 },
                new NationalFocus("nl_dykes", "Ligne d'Eau", "Défenses hydrauliques", 6)
                    { stabilityChange = 0.05f, militaryFactoryReward = 1 },
                new NationalFocus("nl_banking", "Banque d'Amsterdam", "Centre financier européen", 8)
                    { goldReward = 500f, civilianFactoryReward = 2, prerequisites = new[] { "nl_trade" } },
                new NationalFocus("nl_orange", "Maison d'Orange", "Restauration monarchique", 10)
                    { stabilityChange = 0.10f, ppReward = 60f, prerequisites = new[] { "nl_dykes" } },
            };

            // === BAVARIA (4 focuses) ===
            focusTrees[FactionType.Bavaria] = new List<NationalFocus>
            {
                new NationalFocus("bv_napoleon", "Allié de Napoléon", "Alliance avec la France", 6)
                    { goldReward = 300f, stabilityChange = 0.05f },
                new NationalFocus("bv_kingdom", "Royaume de Bavière", "Élévation au rang de royaume", 8)
                    { stabilityChange = 0.10f, ppReward = 50f, prerequisites = new[] { "bv_napoleon" } },
                new NationalFocus("bv_army", "Armée Bavaroise", "Réforme militaire", 8)
                    { militaryFactoryReward = 2, manpowerReward = 5000, prerequisites = new[] { "bv_napoleon" } },
                new NationalFocus("bv_independence", "Indépendance Bavaroise", "Se libérer de la tutelle", 10)
                    { warSupportChange = 0.10f, stabilityChange = 0.05f,
                      prerequisites = new[] { "bv_kingdom", "bv_army" } },
            };

            // === SAXONY (4 focuses) ===
            focusTrees[FactionType.Saxony] = new List<NationalFocus>
            {
                new NationalFocus("sx_rhine", "Confédération du Rhin", "Membre de la confédération napoléonienne", 6)
                    { goldReward = 200f, stabilityChange = 0.05f },
                new NationalFocus("sx_industry", "Manufactures Saxonnes", "Industrie de porcelaine et textile", 8)
                    { civilianFactoryReward = 2, goldReward = 300f, prerequisites = new[] { "sx_rhine" } },
                new NationalFocus("sx_army", "Armée Saxonne", "Forces militaires propres", 8)
                    { militaryFactoryReward = 2, manpowerReward = 5000, prerequisites = new[] { "sx_rhine" } },
                new NationalFocus("sx_survival", "Survie Saxonne", "Maintenir le royaume", 10)
                    { stabilityChange = 0.10f, prerequisites = new[] { "sx_industry", "sx_army" } },
            };

            // === VENICE (4 focuses) ===
            focusTrees[FactionType.Venice] = new List<NationalFocus>
            {
                new NationalFocus("vn_republic", "Sérénissime République", "Traditions républicaines", 6)
                    { stabilityChange = 0.05f, goldReward = 300f },
                new NationalFocus("vn_arsenal", "Arsenal de Venise", "Plus grand chantier naval du monde", 8)
                    { navalYardReward = 3, prerequisites = new[] { "vn_republic" } },
                new NationalFocus("vn_adriatic", "Domination Adriatique", "Contrôle de l'Adriatique", 8)
                    { goldReward = 400f, navalYardReward = 1, prerequisites = new[] { "vn_arsenal" } },
                new NationalFocus("vn_trade_empire", "Empire Commercial", "Réseau commercial mondial", 10)
                    { goldReward = 600f, civilianFactoryReward = 2,
                      prerequisites = new[] { "vn_adriatic" } },
            };

            // === SAVOY/SARDINIA (4 focuses) ===
            focusTrees[FactionType.Savoy] = new List<NationalFocus>
            {
                new NationalFocus("sv_piedmont", "Piedmont Industrieux", "Développement du Piémont", 6)
                    { civilianFactoryReward = 2, goldReward = 200f },
                new NationalFocus("sv_army", "Armée Sarde", "Réforme de l'armée piémontaise", 8)
                    { militaryFactoryReward = 2, manpowerReward = 5000, prerequisites = new[] { "sv_piedmont" } },
                new NationalFocus("sv_italia", "Vers l'Italie", "Aspirations à l'unification", 10)
                    { warSupportChange = 0.10f, claimProvince = "lombardy", prerequisites = new[] { "sv_army" } },
                new NationalFocus("sv_risorgimento", "Risorgimento", "Renaissance italienne", 12)
                    { stabilityChange = 0.10f, manpowerReward = 10000,
                      prerequisites = new[] { "sv_italia" } },
            };

            // === PAPAL STATES (4 focuses) ===
            focusTrees[FactionType.PapalStates] = new List<NationalFocus>
            {
                new NationalFocus("pp_faith", "Défenseur de la Foi", "Autorité spirituelle universelle", 6)
                    { stabilityChange = 0.10f, ppReward = 80f },
                new NationalFocus("pp_diplomacy", "Diplomatie Papale", "Médiation entre les puissances", 8)
                    { ppReward = 100f, goldReward = 300f, prerequisites = new[] { "pp_faith" } },
                new NationalFocus("pp_swiss", "Garde Suisse", "Mercenaires d'élite", 8)
                    { militaryFactoryReward = 1, manpowerReward = 3000, prerequisites = new[] { "pp_faith" } },
                new NationalFocus("pp_temporal", "Pouvoir Temporel", "Maintenir les États Pontificaux", 10)
                    { stabilityChange = 0.10f, goldReward = 400f,
                      prerequisites = new[] { "pp_diplomacy", "pp_swiss" } },
            };

            // === SWITZERLAND (4 focuses) ===
            focusTrees[FactionType.Switzerland] = new List<NationalFocus>
            {
                new NationalFocus("ch_neutral", "Neutralité Éternelle", "Neutralité armée suisse", 6)
                    { stabilityChange = 0.10f },
                new NationalFocus("ch_militia", "Milice Fédérale", "Chaque citoyen est un soldat", 8)
                    { manpowerReward = 8000, militaryFactoryReward = 1, prerequisites = new[] { "ch_neutral" } },
                new NationalFocus("ch_banking", "Banques Suisses", "Secret bancaire et prospérité", 8)
                    { goldReward = 600f, civilianFactoryReward = 1, prerequisites = new[] { "ch_neutral" } },
                new NationalFocus("ch_fortress", "Forteresse Alpine", "Défenses montagnardes imprenables", 10)
                    { stabilityChange = 0.05f, militaryFactoryReward = 2,
                      prerequisites = new[] { "ch_militia" } },
            };

            // Minor factions with minimal trees
            foreach (var ft in new[] { FactionType.Genoa, FactionType.Tuscany, FactionType.Hanover,
                                       FactionType.Modena, FactionType.Parma, FactionType.Lorraine })
            {
                string prefix = ft.ToString().ToLower().Substring(0, 2);
                focusTrees[ft] = new List<NationalFocus>
                {
                    new NationalFocus($"{prefix}_survive", "Survie Nationale", "Maintenir l'indépendance", 6)
                        { stabilityChange = 0.05f, ppReward = 30f },
                    new NationalFocus($"{prefix}_develop", "Développement", "Construire l'économie", 8)
                        { civilianFactoryReward = 1, goldReward = 200f, prerequisites = new[] { $"{prefix}_survive" } },
                    new NationalFocus($"{prefix}_army", "Forces Armées", "Petite armée professionnelle", 8)
                        { militaryFactoryReward = 1, manpowerReward = 3000, prerequisites = new[] { $"{prefix}_survive" } },
                    new NationalFocus($"{prefix}_patron", "Grand Patron", "S'allier à une grande puissance", 10)
                        { ppReward = 50f, goldReward = 300f, stabilityChange = 0.05f,
                          prerequisites = new[] { $"{prefix}_develop", $"{prefix}_army" } },
                };
            }
        }

        public static List<NationalFocus> GetFocusTree(FactionType faction)
        {
            return focusTrees.ContainsKey(faction) ? focusTrees[faction] : new List<NationalFocus>();
        }

        public static bool StartFocus(FactionType faction, string focusId)
        {
            if (activeFocus.ContainsKey(faction) && !string.IsNullOrEmpty(activeFocus[faction]))
                return false; // Already have an active focus

            var tree = GetFocusTree(faction);
            var focus = tree.Find(f => f.focusId == focusId);
            if (focus == null || focus.isCompleted) return false;

            // Check prerequisites
            foreach (string prereq in focus.prerequisites)
            {
                var prereqFocus = tree.Find(f => f.focusId == prereq);
                if (prereqFocus == null || !prereqFocus.isCompleted) return false;
            }

            // Check mutually exclusive focuses
            if (focus.mutuallyExclusive != null)
            {
                foreach (string excl in focus.mutuallyExclusive)
                {
                    var exclFocus = tree.Find(f => f.focusId == excl);
                    if (exclFocus != null && exclFocus.isCompleted) return false;
                }
            }

            activeFocus[faction] = focusId;
            focusProgress[faction] = 0;
            return true;
        }

        /// <summary>Advance focus by one turn. Returns completed focus or null.</summary>
        public static NationalFocus AdvanceFocus(FactionType faction)
        {
            if (!activeFocus.ContainsKey(faction) || string.IsNullOrEmpty(activeFocus[faction]))
                return null;

            if (!focusProgress.ContainsKey(faction)) focusProgress[faction] = 0;
            focusProgress[faction]++;

            var tree = GetFocusTree(faction);
            var focus = tree.Find(f => f.focusId == activeFocus[faction]);
            if (focus == null) return null;

            if (focusProgress[faction] >= focus.turnsToComplete)
            {
                focus.isCompleted = true;
                activeFocus[faction] = null;
                focusProgress[faction] = 0;
                return focus;
            }
            return null;
        }

        /// <summary>Apply focus rewards to faction</summary>
        public static void ApplyFocusRewards(FactionData faction, NationalFocus focus)
        {
            faction.gold += focus.goldReward;
            faction.manpower += (int)focus.manpowerReward;
            faction.stability = Mathf.Clamp01(faction.stability + focus.stabilityChange);
            faction.warSupport = Mathf.Clamp01(faction.warSupport + focus.warSupportChange);
            faction.civilianFactories += focus.civilianFactoryReward;
            faction.militaryFactories += focus.militaryFactoryReward;
            faction.navalYards += focus.navalYardReward;
            faction.politicalPower += focus.ppReward;

            // Permanent bonuses are tracked per-focus and applied via GetTotalResearchBonus/GetTotalProductionBonus
        }

        /// <summary>Get total permanent research speed bonus from completed focuses</summary>
        public static float GetTotalResearchBonus(FactionType faction)
        {
            float total = 0f;
            var tree = GetFocusTree(faction);
            foreach (var f in tree)
                if (f.isCompleted) total += f.researchSpeedBonus;
            return total;
        }

        /// <summary>Get total permanent production bonus from completed focuses</summary>
        public static float GetTotalProductionBonus(FactionType faction)
        {
            float total = 0f;
            var tree = GetFocusTree(faction);
            foreach (var f in tree)
                if (f.isCompleted) total += f.productionBonus;
            return total;
        }

        /// <summary>Get all province claims from completed focuses</summary>
        public static List<string> GetProvinceClaims(FactionType faction)
        {
            var claims = new List<string>();
            var tree = GetFocusTree(faction);
            foreach (var f in tree)
                if (f.isCompleted && !string.IsNullOrEmpty(f.claimProvince))
                    claims.Add(f.claimProvince);
            return claims;
        }

        /// <summary>Get count of completed focuses for a faction</summary>
        public static int GetCompletedCount(FactionType faction)
        {
            int count = 0;
            var tree = GetFocusTree(faction);
            foreach (var f in tree)
                if (f.isCompleted) count++;
            return count;
        }

        public static string GetActiveFocus(FactionType faction)
        {
            return activeFocus.ContainsKey(faction) ? activeFocus[faction] : null;
        }

        public static int GetFocusProgress(FactionType faction)
        {
            return focusProgress.ContainsKey(faction) ? focusProgress[faction] : 0;
        }
    }
}
