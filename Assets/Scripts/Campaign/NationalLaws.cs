using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// National Laws system — 8 categories of laws that can be changed
    /// by spending Political Power. Each law has trade-offs.
    /// </summary>

    // === NEW LAW ENUMS ===

    public enum ReligionLaw
    {
        Theocracy,          // Church controls state: +order, -research, -tolerance
        StateReligion,      // Official religion with tolerance: balanced
        ReligiousTolerance, // Tolerant: +stability, +assimilation, -order in fanatic provinces
        Secularism          // Separation of church and state: +research, +stability, -order
    }

    public enum LandLaw
    {
        Feudalism,          // Lords own land: +order, -food, -pop growth
        SerfdomReform,      // Gradual reform: balanced
        AgrarianReform,     // Land redistribution: +food, +pop growth, -order temporarily
        FreeProperty        // Private property: +gold, +food, +pop growth, -loyalty in feudal areas
    }

    public enum PressLaw
    {
        TotalCensorship,    // No free press: +order, -PP gain, -research
        StateCensorship,    // Controlled press: slight order bonus, neutral
        LimitedFreedom,     // Some freedom: +PP gain, +research, slight unrest
        FreePressComplete   // Total freedom: +PP gain, +research, +unrest risk
    }

    public enum EducationLaw
    {
        NoPublicEducation,  // No system: cheap, low research, low quality manpower
        ReligiousSchools,   // Church-run schools: +order, +slight research
        PrimaryEducation,   // Basic schooling: +research, +manpower quality
        UniversalEducation  // Full education: +research, +PP, expensive
    }

    public enum SlaveryLaw
    {
        Slavery,            // Slave labor: +production, -stability, -diplomacy
        Serfdom,            // Bound labor: +food, -stability, -diplomacy
        Indentured,         // Contract labor: slight production bonus
        Abolition           // Free labor: +stability, +diplomacy, -cheap labor
    }

    [System.Serializable]
    public class NationalLawSet
    {
        public ConscriptionLaw conscription = ConscriptionLaw.LimitedConscription;
        public EconomyLaw economy = EconomyLaw.CivilianEconomy;
        public TradeLaw trade = TradeLaw.LimitedExports;
        public ReligionLaw religion = ReligionLaw.StateReligion;
        public LandLaw land = LandLaw.Feudalism;
        public PressLaw press = PressLaw.StateCensorship;
        public EducationLaw education = EducationLaw.ReligiousSchools;
        public SlaveryLaw slavery = SlaveryLaw.Serfdom;
    }

    public static class NationalLaws
    {
        // Per-faction law sets
        private static Dictionary<FactionType, NationalLawSet> lawSets = 
            new Dictionary<FactionType, NationalLawSet>();

        // Transition cooldowns (turns remaining before a law category can be changed again)
        private static Dictionary<FactionType, Dictionary<string, int>> cooldowns =
            new Dictionary<FactionType, Dictionary<string, int>>();

        public static void Initialize(FactionType faction)
        {
            if (!lawSets.ContainsKey(faction))
                lawSets[faction] = new NationalLawSet();
            if (!cooldowns.ContainsKey(faction))
                cooldowns[faction] = new Dictionary<string, int>();
        }

        public static NationalLawSet GetLaws(FactionType faction)
        {
            Initialize(faction);
            return lawSets[faction];
        }

        /// <summary>Process law cooldowns at end of turn</summary>
        public static void ProcessTurnCooldowns(FactionType faction)
        {
            Initialize(faction);
            var cd = cooldowns[faction];
            var keys = new List<string>(cd.Keys);
            foreach (var key in keys)
            {
                cd[key]--;
                if (cd[key] <= 0) cd.Remove(key);
            }
        }

        public static bool IsOnCooldown(FactionType faction, string category)
        {
            Initialize(faction);
            return cooldowns[faction].ContainsKey(category) && cooldowns[faction][category] > 0;
        }

        public static int GetCooldownRemaining(FactionType faction, string category)
        {
            Initialize(faction);
            return cooldowns[faction].ContainsKey(category) ? cooldowns[faction][category] : 0;
        }

        private static void SetCooldown(FactionType faction, string category, int turns)
        {
            Initialize(faction);
            cooldowns[faction][category] = turns;
        }

        // === PP COSTS ===

        public static float GetConscriptionCost(ConscriptionLaw from, ConscriptionLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 50f + diff * 75f;
        }

        public static float GetEconomyCost(EconomyLaw from, EconomyLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 50f + diff * 100f;
        }

        public static float GetTradeCost(TradeLaw from, TradeLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 30f + diff * 50f;
        }

        public static float GetReligionCost(ReligionLaw from, ReligionLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 75f + diff * 100f; // Religion changes are expensive
        }

        public static float GetLandCost(LandLaw from, LandLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 60f + diff * 80f;
        }

        public static float GetPressCost(PressLaw from, PressLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 40f + diff * 50f;
        }

        public static float GetEducationCost(EducationLaw from, EducationLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 50f + diff * 70f;
        }

        public static float GetSlaveryCost(SlaveryLaw from, SlaveryLaw to)
        {
            int diff = Mathf.Abs((int)to - (int)from);
            return 80f + diff * 120f; // Very politically costly
        }

        // === CHANGE LAWS ===

        public static bool TryChangeConscription(FactionData faction, ConscriptionLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "conscription")) return false;
            float cost = GetConscriptionCost(faction.conscriptionLaw, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                faction.conscriptionLaw = newLaw;
                SetCooldown(faction.factionType, "conscription", 3);
                return true;
            }
            return false;
        }

        public static bool TryChangeEconomy(FactionData faction, EconomyLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "economy")) return false;
            float cost = GetEconomyCost(faction.economyLaw, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                faction.economyLaw = newLaw;
                SetCooldown(faction.factionType, "economy", 3);
                return true;
            }
            return false;
        }

        public static bool TryChangeTrade(FactionData faction, TradeLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "trade")) return false;
            float cost = GetTradeCost(faction.tradeLaw, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                faction.tradeLaw = newLaw;
                SetCooldown(faction.factionType, "trade", 2);
                return true;
            }
            return false;
        }

        public static bool TryChangeReligion(FactionData faction, ReligionLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "religion")) return false;
            var laws = GetLaws(faction.factionType);
            float cost = GetReligionCost(laws.religion, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                laws.religion = newLaw;
                SetCooldown(faction.factionType, "religion", 5);
                return true;
            }
            return false;
        }

        public static bool TryChangeLand(FactionData faction, LandLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "land")) return false;
            var laws = GetLaws(faction.factionType);
            float cost = GetLandCost(laws.land, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                laws.land = newLaw;
                SetCooldown(faction.factionType, "land", 4);
                return true;
            }
            return false;
        }

        public static bool TryChangePress(FactionData faction, PressLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "press")) return false;
            var laws = GetLaws(faction.factionType);
            float cost = GetPressCost(laws.press, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                laws.press = newLaw;
                SetCooldown(faction.factionType, "press", 3);
                return true;
            }
            return false;
        }

        public static bool TryChangeEducation(FactionData faction, EducationLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "education")) return false;
            var laws = GetLaws(faction.factionType);
            float cost = GetEducationCost(laws.education, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                laws.education = newLaw;
                SetCooldown(faction.factionType, "education", 4);
                return true;
            }
            return false;
        }

        public static bool TryChangeSlavery(FactionData faction, SlaveryLaw newLaw)
        {
            if (IsOnCooldown(faction.factionType, "slavery")) return false;
            var laws = GetLaws(faction.factionType);
            float cost = GetSlaveryCost(laws.slavery, newLaw);
            if (faction.SpendPoliticalPower(cost))
            {
                laws.slavery = newLaw;
                SetCooldown(faction.factionType, "slavery", 5);
                // Abolishing slavery improves diplomacy with progressive nations
                if (newLaw == SlaveryLaw.Abolition)
                {
                    var diplomacy = DiplomacySystem.Instance;
                    if (diplomacy != null)
                    {
                        var cm = CampaignManager.Instance;
                        if (cm != null)
                        {
                            foreach (var other in cm.Factions.Keys)
                            {
                                if (other == faction.factionType) continue;
                                diplomacy.ModifyRelationScore(faction.factionType, other, 5);
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }

        // === EFFECT CALCULATIONS ===

        /// <summary>Religion law effect on province unrest</summary>
        public static float GetReligionUnrestModifier(ReligionLaw law) => law switch
        {
            ReligionLaw.Theocracy => -3f,          // Reduces unrest for same-religion
            ReligionLaw.StateReligion => -1f,
            ReligionLaw.ReligiousTolerance => 0f,
            ReligionLaw.Secularism => 1f,           // Slight unrest from conservatives
            _ => 0f
        };

        /// <summary>Religion law effect on religious unrest for foreign religion provinces</summary>
        public static float GetReligionToleranceModifier(ReligionLaw law) => law switch
        {
            ReligionLaw.Theocracy => 5f,            // High unrest for other religions
            ReligionLaw.StateReligion => 2f,
            ReligionLaw.ReligiousTolerance => -2f,  // Reduces foreign religion unrest
            ReligionLaw.Secularism => -3f,           // Greatly reduces religious unrest
            _ => 0f
        };

        /// <summary>Religion law effect on research speed</summary>
        public static float GetReligionResearchModifier(ReligionLaw law) => law switch
        {
            ReligionLaw.Theocracy => -0.15f,
            ReligionLaw.StateReligion => 0f,
            ReligionLaw.ReligiousTolerance => 0.05f,
            ReligionLaw.Secularism => 0.15f,
            _ => 0f
        };

        /// <summary>Land law effect on food production</summary>
        public static float GetLandFoodModifier(LandLaw law) => law switch
        {
            LandLaw.Feudalism => -0.10f,
            LandLaw.SerfdomReform => 0f,
            LandLaw.AgrarianReform => 0.15f,
            LandLaw.FreeProperty => 0.20f,
            _ => 0f
        };

        /// <summary>Land law effect on population growth</summary>
        public static float GetLandPopGrowthModifier(LandLaw law) => law switch
        {
            LandLaw.Feudalism => -0.10f,
            LandLaw.SerfdomReform => 0f,
            LandLaw.AgrarianReform => 0.10f,
            LandLaw.FreeProperty => 0.15f,
            _ => 0f
        };

        /// <summary>Land law effect on gold income</summary>
        public static float GetLandGoldModifier(LandLaw law) => law switch
        {
            LandLaw.Feudalism => 0f,
            LandLaw.SerfdomReform => 0.05f,
            LandLaw.AgrarianReform => 0.05f,
            LandLaw.FreeProperty => 0.15f,
            _ => 0f
        };

        /// <summary>Press law effect on PP gain per turn</summary>
        public static float GetPressPPModifier(PressLaw law) => law switch
        {
            PressLaw.TotalCensorship => -0.5f,
            PressLaw.StateCensorship => 0f,
            PressLaw.LimitedFreedom => 0.5f,
            PressLaw.FreePressComplete => 1.0f,
            _ => 0f
        };

        /// <summary>Press law effect on research speed</summary>
        public static float GetPressResearchModifier(PressLaw law) => law switch
        {
            PressLaw.TotalCensorship => -0.10f,
            PressLaw.StateCensorship => 0f,
            PressLaw.LimitedFreedom => 0.05f,
            PressLaw.FreePressComplete => 0.10f,
            _ => 0f
        };

        /// <summary>Press law effect on unrest</summary>
        public static float GetPressUnrestModifier(PressLaw law) => law switch
        {
            PressLaw.TotalCensorship => -2f,
            PressLaw.StateCensorship => 0f,
            PressLaw.LimitedFreedom => 1f,
            PressLaw.FreePressComplete => 2f,
            _ => 0f
        };

        /// <summary>Education law effect on research speed</summary>
        public static float GetEducationResearchModifier(EducationLaw law) => law switch
        {
            EducationLaw.NoPublicEducation => -0.15f,
            EducationLaw.ReligiousSchools => 0f,
            EducationLaw.PrimaryEducation => 0.15f,
            EducationLaw.UniversalEducation => 0.30f,
            _ => 0f
        };

        /// <summary>Education law effect on PP gain</summary>
        public static float GetEducationPPModifier(EducationLaw law) => law switch
        {
            EducationLaw.NoPublicEducation => 0f,
            EducationLaw.ReligiousSchools => 0f,
            EducationLaw.PrimaryEducation => 0.3f,
            EducationLaw.UniversalEducation => 0.8f,
            _ => 0f
        };

        /// <summary>Education law gold cost per turn (maintenance)</summary>
        public static float GetEducationCostPerTurn(EducationLaw law) => law switch
        {
            EducationLaw.NoPublicEducation => 0f,
            EducationLaw.ReligiousSchools => 10f,
            EducationLaw.PrimaryEducation => 30f,
            EducationLaw.UniversalEducation => 60f,
            _ => 0f
        };

        /// <summary>Slavery law effect on production</summary>
        public static float GetSlaveryProductionModifier(SlaveryLaw law) => law switch
        {
            SlaveryLaw.Slavery => 0.20f,
            SlaveryLaw.Serfdom => 0.10f,
            SlaveryLaw.Indentured => 0.05f,
            SlaveryLaw.Abolition => 0f,
            _ => 0f
        };

        /// <summary>Slavery law effect on stability</summary>
        public static float GetSlaveryStabilityModifier(SlaveryLaw law) => law switch
        {
            SlaveryLaw.Slavery => -0.10f,
            SlaveryLaw.Serfdom => -0.05f,
            SlaveryLaw.Indentured => 0f,
            SlaveryLaw.Abolition => 0.05f,
            _ => 0f
        };

        /// <summary>Slavery law diplomatic penalty</summary>
        public static int GetSlaveryDiplomacyPenalty(SlaveryLaw law) => law switch
        {
            SlaveryLaw.Slavery => -15,
            SlaveryLaw.Serfdom => -5,
            SlaveryLaw.Indentured => 0,
            SlaveryLaw.Abolition => 5,
            _ => 0
        };

        // === DISPLAY HELPERS ===
        public static string GetConscriptionName(ConscriptionLaw law) => law switch
        {
            ConscriptionLaw.Volunteer => "Volontariat (1%)",
            ConscriptionLaw.LimitedConscription => "Conscription limitée (2.5%)",
            ConscriptionLaw.ExtendedConscription => "Conscription étendue (5%)",
            ConscriptionLaw.ServiceByRequirement => "Service obligatoire (8%)",
            ConscriptionLaw.TotalMobilization => "Mobilisation totale (15%)",
            _ => law.ToString()
        };

        public static string GetEconomyName(EconomyLaw law) => law switch
        {
            EconomyLaw.CivilianEconomy => "Économie civile",
            EconomyLaw.PreMobilization => "Pré-mobilisation (+10%)",
            EconomyLaw.WarEconomy => "Économie de guerre (+25%)",
            EconomyLaw.TotalWar => "Guerre totale (+50%)",
            _ => law.ToString()
        };

        public static string GetTradeName(TradeLaw law) => law switch
        {
            TradeLaw.FreeTradePolicy => "Libre-échange (+15% rech.)",
            TradeLaw.ExportFocus => "Export focus (+5% rech.)",
            TradeLaw.LimitedExports => "Exports limités",
            TradeLaw.ClosedEconomy => "Économie fermée (+20% ress.)",
            _ => law.ToString()
        };

        public static string GetReligionLawName(ReligionLaw law) => law switch
        {
            ReligionLaw.Theocracy => "Théocratie",
            ReligionLaw.StateReligion => "Religion d'État",
            ReligionLaw.ReligiousTolerance => "Tolérance religieuse",
            ReligionLaw.Secularism => "Sécularisme",
            _ => law.ToString()
        };

        public static string GetReligionLawDesc(ReligionLaw law) => law switch
        {
            ReligionLaw.Theocracy => "L'Église contrôle l'État. +Ordre, -Recherche, forte intolérance",
            ReligionLaw.StateReligion => "Religion officielle avec tolérance limitée",
            ReligionLaw.ReligiousTolerance => "Toutes les religions tolérées. +Stabilité, +Assimilation",
            ReligionLaw.Secularism => "Séparation Église-État. +Recherche, +Stabilité",
            _ => ""
        };

        public static string GetLandLawName(LandLaw law) => law switch
        {
            LandLaw.Feudalism => "Féodalisme",
            LandLaw.SerfdomReform => "Réforme du servage",
            LandLaw.AgrarianReform => "Réforme agraire",
            LandLaw.FreeProperty => "Propriété libre",
            _ => law.ToString()
        };

        public static string GetLandLawDesc(LandLaw law) => law switch
        {
            LandLaw.Feudalism => "Les seigneurs possèdent la terre. +Ordre, -Nourriture, -Croissance",
            LandLaw.SerfdomReform => "Réforme progressive. Effets équilibrés",
            LandLaw.AgrarianReform => "Redistribution des terres. +Nourriture, +Croissance, -Ordre temporaire",
            LandLaw.FreeProperty => "Propriété privée. +Or, +Nourriture, +Croissance",
            _ => ""
        };

        public static string GetPressLawName(PressLaw law) => law switch
        {
            PressLaw.TotalCensorship => "Censure totale",
            PressLaw.StateCensorship => "Presse contrôlée",
            PressLaw.LimitedFreedom => "Liberté limitée",
            PressLaw.FreePressComplete => "Presse libre",
            _ => law.ToString()
        };

        public static string GetPressLawDesc(PressLaw law) => law switch
        {
            PressLaw.TotalCensorship => "Aucune liberté d'expression. +Ordre, -PP/tour, -Recherche",
            PressLaw.StateCensorship => "Presse sous contrôle de l'État. Effets neutres",
            PressLaw.LimitedFreedom => "Liberté partielle. +PP/tour, +Recherche, léger désordre",
            PressLaw.FreePressComplete => "Liberté totale. +PP/tour, +Recherche, risque de troubles",
            _ => ""
        };

        public static string GetEducationLawName(EducationLaw law) => law switch
        {
            EducationLaw.NoPublicEducation => "Aucune éducation publique",
            EducationLaw.ReligiousSchools => "Écoles religieuses",
            EducationLaw.PrimaryEducation => "Éducation primaire",
            EducationLaw.UniversalEducation => "Éducation universelle",
            _ => law.ToString()
        };

        public static string GetEducationLawDesc(EducationLaw law) => law switch
        {
            EducationLaw.NoPublicEducation => "Pas de système éducatif. Gratuit mais manpower de mauvaise qualité",
            EducationLaw.ReligiousSchools => "Écoles gérées par l'Église. +Ordre, légère recherche",
            EducationLaw.PrimaryEducation => "Éducation de base pour tous. +Recherche, +Qualité manpower",
            EducationLaw.UniversalEducation => "Éducation complète. +Recherche ++, +PP, coûteux",
            _ => ""
        };

        public static string GetSlaveryLawName(SlaveryLaw law) => law switch
        {
            SlaveryLaw.Slavery => "Esclavage",
            SlaveryLaw.Serfdom => "Servage",
            SlaveryLaw.Indentured => "Travail contractuel",
            SlaveryLaw.Abolition => "Abolition",
            _ => law.ToString()
        };

        public static string GetSlaveryLawDesc(SlaveryLaw law) => law switch
        {
            SlaveryLaw.Slavery => "Main-d'œuvre esclave. +Production, -Stabilité, -Diplomatie",
            SlaveryLaw.Serfdom => "Paysans liés à la terre. +Nourriture, -Stabilité, -Diplomatie",
            SlaveryLaw.Indentured => "Travail sous contrat. Léger bonus production",
            SlaveryLaw.Abolition => "Travail libre. +Stabilité, +Diplomatie",
            _ => ""
        };
    }
}
