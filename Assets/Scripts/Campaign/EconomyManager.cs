using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Advanced economy system: budget allocation, national debt, inflation,
    /// extended resources, and state monopolies.
    /// </summary>

    public enum BudgetCategory
    {
        Military,       // Army maintenance, recruitment
        Navy,           // Fleet maintenance, shipbuilding
        Administration, // Government costs, corruption reduction
        Infrastructure, // Building construction speed
        Research        // Research speed bonus
    }

    // ResourceType is defined in ProductionChain.cs — Coal and Saltpetre added there

    [System.Serializable]
    public class BudgetAllocation
    {
        public float military = 0.35f;       // 35%
        public float navy = 0.10f;           // 10%
        public float administration = 0.20f; // 20%
        public float infrastructure = 0.15f; // 15%
        public float research = 0.20f;       // 20%

        public void Normalize()
        {
            float total = military + navy + administration + infrastructure + research;
            if (total <= 0) { military = 0.2f; navy = 0.2f; administration = 0.2f; infrastructure = 0.2f; research = 0.2f; return; }
            military /= total;
            navy /= total;
            administration /= total;
            infrastructure /= total;
            research /= total;
        }

        public float Get(BudgetCategory cat) => cat switch
        {
            BudgetCategory.Military => military,
            BudgetCategory.Navy => navy,
            BudgetCategory.Administration => administration,
            BudgetCategory.Infrastructure => infrastructure,
            BudgetCategory.Research => research,
            _ => 0f
        };

        public void Set(BudgetCategory cat, float value)
        {
            switch (cat)
            {
                case BudgetCategory.Military: military = value; break;
                case BudgetCategory.Navy: navy = value; break;
                case BudgetCategory.Administration: administration = value; break;
                case BudgetCategory.Infrastructure: infrastructure = value; break;
                case BudgetCategory.Research: research = value; break;
            }
            Normalize();
        }
    }

    [System.Serializable]
    public class ExtendedResources
    {
        public float wood = 200f;
        public float horses = 50f;
        public float textiles = 100f;
        public float coal = 50f;
        public float saltpetre = 80f;

        public float Get(ResourceType type) => type switch
        {
            ResourceType.Wood => wood,
            ResourceType.Horses => horses,
            ResourceType.Textiles => textiles,
            ResourceType.Coal => coal,
            ResourceType.Saltpetre => saltpetre,
            _ => 0f
        };

        public void Add(ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Wood: wood += amount; break;
                case ResourceType.Horses: horses += amount; break;
                case ResourceType.Textiles: textiles += amount; break;
                case ResourceType.Coal: coal += amount; break;
                case ResourceType.Saltpetre: saltpetre += amount; break;
            }
        }

        public bool Consume(ResourceType type, float amount)
        {
            float current = Get(type);
            if (current < amount) return false;
            Add(type, -amount);
            return true;
        }
    }

    [System.Serializable]
    public class NationalDebt
    {
        public float totalDebt = 0f;
        public float interestRate = 0.05f;  // 5% per turn
        public float maxDebtMultiplier = 5f; // Max debt = 5x annual income

        public float InterestPayment => totalDebt * interestRate;

        public bool CanBorrow(float amount, float annualIncome)
        {
            return (totalDebt + amount) <= annualIncome * maxDebtMultiplier;
        }

        public void Borrow(float amount)
        {
            totalDebt += amount;
        }

        public void Repay(float amount)
        {
            totalDebt = Mathf.Max(0f, totalDebt - amount);
        }

        /// <summary>Debt level as fraction of max (0-1)</summary>
        public float DebtRatio(float annualIncome)
        {
            float maxDebt = annualIncome * maxDebtMultiplier;
            return maxDebt > 0 ? Mathf.Clamp01(totalDebt / maxDebt) : 0f;
        }
    }

    public static class EconomyManager
    {
        // Per-faction economy data
        private static Dictionary<FactionType, BudgetAllocation> budgets =
            new Dictionary<FactionType, BudgetAllocation>();
        private static Dictionary<FactionType, ExtendedResources> extResources =
            new Dictionary<FactionType, ExtendedResources>();
        private static Dictionary<FactionType, NationalDebt> debts =
            new Dictionary<FactionType, NationalDebt>();
        private static Dictionary<FactionType, float> inflation =
            new Dictionary<FactionType, float>();
        private static Dictionary<FactionType, float> lastTurnIncome =
            new Dictionary<FactionType, float>();
        private static Dictionary<FactionType, float> lastTurnExpenses =
            new Dictionary<FactionType, float>();
        private static Dictionary<FactionType, HashSet<ResourceType>> monopolies =
            new Dictionary<FactionType, HashSet<ResourceType>>();

        public static void Initialize(FactionType faction)
        {
            if (!budgets.ContainsKey(faction))
                budgets[faction] = new BudgetAllocation();
            if (!extResources.ContainsKey(faction))
                extResources[faction] = new ExtendedResources();
            if (!debts.ContainsKey(faction))
                debts[faction] = new NationalDebt();
            if (!inflation.ContainsKey(faction))
                inflation[faction] = 0f;
            if (!lastTurnIncome.ContainsKey(faction))
                lastTurnIncome[faction] = 0f;
            if (!lastTurnExpenses.ContainsKey(faction))
                lastTurnExpenses[faction] = 0f;
            if (!monopolies.ContainsKey(faction))
                monopolies[faction] = new HashSet<ResourceType>();
        }

        // === GETTERS ===
        public static BudgetAllocation GetBudget(FactionType f) { Initialize(f); return budgets[f]; }
        public static ExtendedResources GetExtendedResources(FactionType f) { Initialize(f); return extResources[f]; }
        public static NationalDebt GetDebt(FactionType f) { Initialize(f); return debts[f]; }
        public static float GetInflation(FactionType f) { Initialize(f); return inflation[f]; }
        public static float GetLastIncome(FactionType f) { Initialize(f); return lastTurnIncome[f]; }
        public static float GetLastExpenses(FactionType f) { Initialize(f); return lastTurnExpenses[f]; }
        public static float GetBalance(FactionType f) => GetLastIncome(f) - GetLastExpenses(f);

        // === BUDGET EFFECTS ===

        /// <summary>Military budget modifier (0.5x at 0%, 1.5x at 50%)</summary>
        public static float GetMilitaryBudgetModifier(FactionType f)
        {
            float pct = GetBudget(f).military;
            return 0.5f + pct * 2f;
        }

        /// <summary>Research speed modifier from budget allocation</summary>
        public static float GetResearchBudgetModifier(FactionType f)
        {
            float pct = GetBudget(f).research;
            return 0.5f + pct * 2.5f;
        }

        /// <summary>Construction speed modifier from infrastructure budget</summary>
        public static float GetConstructionBudgetModifier(FactionType f)
        {
            float pct = GetBudget(f).infrastructure;
            return 0.5f + pct * 2f;
        }

        /// <summary>Admin budget reduces corruption (unrest in all provinces)</summary>
        public static float GetCorruptionReduction(FactionType f)
        {
            float pct = GetBudget(f).administration;
            return pct * 10f; // Max -5 unrest at 50% admin budget
        }

        // === INFLATION ===

        /// <summary>Process inflation for a turn. Inflation rises with deficit, falls with surplus.</summary>
        public static void ProcessInflation(FactionType faction, float income, float expenses)
        {
            Initialize(faction);
            float balance = income - expenses;
            float debtRatio = debts[faction].DebtRatio(income * 4f); // Annualize

            // Inflation rises with deficit spending and high debt
            float inflationDelta = 0f;
            if (balance < 0) inflationDelta += Mathf.Abs(balance) * 0.0001f;
            inflationDelta += debtRatio * 0.5f;

            // Inflation naturally decays
            inflationDelta -= 0.2f;

            // Surplus reduces inflation
            if (balance > 0) inflationDelta -= balance * 0.00005f;

            inflation[faction] = Mathf.Clamp(inflation[faction] + inflationDelta, 0f, 50f);
        }

        /// <summary>Inflation makes everything more expensive (1.0 = no inflation, 1.5 = 50% more expensive)</summary>
        public static float GetInflationCostModifier(FactionType f)
        {
            return 1f + GetInflation(f) * 0.01f;
        }

        // === DEBT ===

        /// <summary>Borrow gold, adding to national debt</summary>
        public static bool Borrow(FactionType faction, float amount)
        {
            Initialize(faction);
            float annualIncome = lastTurnIncome[faction] * 4f;
            if (!debts[faction].CanBorrow(amount, annualIncome)) return false;

            debts[faction].Borrow(amount);
            var cm = CampaignManager.Instance;
            if (cm != null && cm.Factions.ContainsKey(faction))
                cm.Factions[faction].gold += amount;

            Debug.Log($"[Economy] {faction} borrowed {amount:F0}g (total debt: {debts[faction].totalDebt:F0})");
            return true;
        }

        /// <summary>Repay debt from treasury</summary>
        public static bool RepayDebt(FactionType faction, float amount)
        {
            Initialize(faction);
            var cm = CampaignManager.Instance;
            if (cm == null || !cm.Factions.ContainsKey(faction)) return false;

            float available = Mathf.Min(amount, cm.Factions[faction].gold);
            available = Mathf.Min(available, debts[faction].totalDebt);
            if (available <= 0) return false;

            cm.Factions[faction].gold -= available;
            debts[faction].Repay(available);
            return true;
        }

        // === MONOPOLIES ===

        /// <summary>Nationalize a resource for +30% income but -10 relations with trade partners</summary>
        public static bool NationalizeResource(FactionType faction, ResourceType resource)
        {
            Initialize(faction);
            if (monopolies[faction].Contains(resource)) return false;

            var cm = CampaignManager.Instance;
            if (cm != null && cm.Factions.ContainsKey(faction))
            {
                if (!cm.Factions[faction].SpendPoliticalPower(75f)) return false;
            }

            monopolies[faction].Add(resource);

            // Reduce relations with trade partners
            var diplomacy = DiplomacySystem.Instance;
            if (diplomacy != null && cm != null)
            {
                foreach (var other in cm.Factions.Keys)
                {
                    if (other == faction) continue;
                    if (diplomacy.HasTreaty(faction, other, TreatyType.TradeAgreement))
                        diplomacy.ModifyRelationScore(faction, other, -10);
                }
            }

            Debug.Log($"[Economy] {faction} nationalized {resource}!");
            return true;
        }

        public static bool HasMonopoly(FactionType faction, ResourceType resource)
        {
            Initialize(faction);
            return monopolies[faction].Contains(resource);
        }

        /// <summary>Get income bonus from monopoly (30% for monopolized resources)</summary>
        public static float GetMonopolyIncomeBonus(FactionType faction)
        {
            Initialize(faction);
            return monopolies[faction].Count * 0.05f; // 5% per monopolized resource
        }

        // === TURN PROCESSING ===

        /// <summary>Process economy for a faction at end of turn</summary>
        public static void ProcessEconomyTurn(FactionType faction, float income, float expenses)
        {
            Initialize(faction);

            lastTurnIncome[faction] = income;
            lastTurnExpenses[faction] = expenses;

            // Pay debt interest
            float interest = debts[faction].InterestPayment;
            if (interest > 0)
            {
                var cm = CampaignManager.Instance;
                if (cm != null && cm.Factions.ContainsKey(faction))
                {
                    if (cm.Factions[faction].gold >= interest)
                    {
                        cm.Factions[faction].gold -= interest;
                    }
                    else
                    {
                        // Can't pay interest — debt grows, stability drops
                        debts[faction].Borrow(interest - cm.Factions[faction].gold);
                        cm.Factions[faction].gold = 0;
                        cm.Factions[faction].stability -= 0.02f;
                    }
                }
            }

            // Process inflation
            ProcessInflation(faction, income, expenses);

            // High debt affects stability
            float debtRatio = debts[faction].DebtRatio(income * 4f);
            if (debtRatio > 0.7f)
            {
                var cm = CampaignManager.Instance;
                if (cm != null && cm.Factions.ContainsKey(faction))
                {
                    cm.Factions[faction].stability -= (debtRatio - 0.7f) * 0.05f;
                    cm.Factions[faction].stability = Mathf.Clamp01(cm.Factions[faction].stability);
                }
            }
        }

        /// <summary>Produce extended resources from provinces for a faction</summary>
        public static void ProduceExtendedResources(FactionType faction, Dictionary<string, ProvinceData> provinces, FactionData fd)
        {
            Initialize(faction);
            var res = extResources[faction];
            float seasonMod = SeasonSystem.GetProductionModifier();

            foreach (string pid in fd.ownedProvinceIds)
            {
                if (!provinces.ContainsKey(pid)) continue;
                var prov = provinces[pid];

                // Wood from forest/hills provinces
                if (prov.terrainType == ProvinceTerrainType.Forest)
                    res.wood += 15f * seasonMod;
                else if (prov.terrainType == ProvinceTerrainType.Hills)
                    res.wood += 5f * seasonMod;

                // Horses from plains/stables
                if (prov.terrainType == ProvinceTerrainType.Plains)
                    res.horses += 3f * seasonMod;
                foreach (var b in prov.buildings)
                    if (b.type == BuildingType.Stables) res.horses += 5f * b.level * seasonMod;

                // Textiles from markets in high-pop provinces
                foreach (var b in prov.buildings)
                    if (b.type == BuildingType.Market) res.textiles += 4f * b.level * seasonMod;

                // Coal from mines
                foreach (var b in prov.buildings)
                    if (b.type == BuildingType.Mine) res.coal += 3f * b.level * seasonMod;

                // Saltpetre from special provinces
                if (prov.terrainType == ProvinceTerrainType.Desert ||
                    prov.terrainType == ProvinceTerrainType.Mountains)
                    res.saltpetre += 5f * seasonMod;
            }

            // Monopoly bonus
            float monopolyBonus = 1f + GetMonopolyIncomeBonus(faction);
            res.wood *= monopolyBonus;
            res.horses *= monopolyBonus;
            res.textiles *= monopolyBonus;
            res.coal *= monopolyBonus;
            res.saltpetre *= monopolyBonus;

            // Consume saltpetre for gunpowder production
            float gunpowderDemand = fd.armyIds.Count * 5f;
            if (res.saltpetre < gunpowderDemand)
            {
                // Low saltpetre = equipment shortage
                Debug.Log($"[Economy] {faction} low on saltpetre! ({res.saltpetre:F0}/{gunpowderDemand:F0})");
            }
        }

        // === UI HELPERS ===

        public static string GetBudgetCategoryName(BudgetCategory cat) => cat switch
        {
            BudgetCategory.Military => "Militaire",
            BudgetCategory.Navy => "Marine",
            BudgetCategory.Administration => "Administration",
            BudgetCategory.Infrastructure => "Infrastructure",
            BudgetCategory.Research => "Recherche",
            _ => "?"
        };

        public static string GetResourceName(ResourceType type) => type switch
        {
            ResourceType.Gold => "Or",
            ResourceType.Food => "Nourriture",
            ResourceType.Iron => "Fer",
            ResourceType.Wood => "Bois",
            ResourceType.Horses => "Chevaux",
            ResourceType.Textiles => "Textiles",
            ResourceType.Coal => "Charbon",
            ResourceType.Saltpetre => "Salpêtre",
            _ => "?"
        };

        public static Color GetResourceColor(ResourceType type) => type switch
        {
            ResourceType.Gold => new Color(1f, 0.85f, 0.2f),
            ResourceType.Food => new Color(0.4f, 0.8f, 0.3f),
            ResourceType.Iron => new Color(0.6f, 0.6f, 0.7f),
            ResourceType.Wood => new Color(0.6f, 0.4f, 0.2f),
            ResourceType.Horses => new Color(0.8f, 0.6f, 0.3f),
            ResourceType.Textiles => new Color(0.7f, 0.4f, 0.7f),
            ResourceType.Coal => new Color(0.3f, 0.3f, 0.3f),
            ResourceType.Saltpetre => new Color(0.9f, 0.9f, 0.6f),
            _ => Color.white
        };
    }
}
