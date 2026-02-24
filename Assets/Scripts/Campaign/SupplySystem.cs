using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// HoI4-style supply system. Each province has supply capacity based on infrastructure.
    /// Armies consume supply proportional to their size. Over-supply causes attrition.
    /// </summary>
    
    [System.Serializable]
    public class ProvinceSupplyData
    {
        public string provinceId;
        public int infrastructureLevel = 3;   // 1-10
        public float supplyCapacity;           // Max supply units
        public float currentDemand;            // Supply consumed by armies
        public bool hasSupplyDepot = false;

        public float SupplyRatio => supplyCapacity > 0 ? Mathf.Clamp01(supplyCapacity / Mathf.Max(1f, currentDemand)) : 1f;
        public bool IsOverSupplied => currentDemand > supplyCapacity;

        public ProvinceSupplyData(string id, int infraLevel = 3)
        {
            provinceId = id;
            infrastructureLevel = infraLevel;
            RecalculateCapacity();
        }

        public void RecalculateCapacity()
        {
            // Base capacity from infrastructure (exponential scaling)
            supplyCapacity = infrastructureLevel * infrastructureLevel * 100f;
            if (hasSupplyDepot) supplyCapacity *= 1.5f;
        }
    }

    public static class SupplySystem
    {
        private static Dictionary<string, ProvinceSupplyData> provinceSupply = 
            new Dictionary<string, ProvinceSupplyData>();

        public static void Initialize(Dictionary<string, ProvinceData> provinces)
        {
            foreach (var kvp in provinces)
            {
                if (!provinceSupply.ContainsKey(kvp.Key))
                    provinceSupply[kvp.Key] = new ProvinceSupplyData(kvp.Key);
            }
        }

        public static ProvinceSupplyData GetSupplyData(string provinceId)
        {
            if (!provinceSupply.ContainsKey(provinceId))
                provinceSupply[provinceId] = new ProvinceSupplyData(provinceId);
            return provinceSupply[provinceId];
        }

        /// <summary>Calculate supply demand for all armies and apply attrition</summary>
        public static void ProcessSupplyTurn(Dictionary<string, ArmyData> armies, Dictionary<FactionType, FactionData> factions)
        {
            // Reset demands
            foreach (var kvp in provinceSupply)
                kvp.Value.currentDemand = 0;

            // Calculate demand per province
            foreach (var kvp in armies)
            {
                ArmyData army = kvp.Value;
                string provId = army.currentProvinceId;
                if (string.IsNullOrEmpty(provId)) continue;

                float demand = CalculateArmySupplyDemand(army);
                var supplyData = GetSupplyData(provId);
                supplyData.currentDemand += demand;
            }

            // Apply attrition to armies in over-supplied provinces
            foreach (var kvp in armies)
            {
                ArmyData army = kvp.Value;
                string provId = army.currentProvinceId;
                if (string.IsNullOrEmpty(provId)) continue;

                var supplyData = GetSupplyData(provId);
                if (supplyData.IsOverSupplied)
                {
                    float excessRatio = 1f - supplyData.SupplyRatio;
                    ApplyAttrition(army, excessRatio, factions);
                }
            }
        }

        public static float CalculateArmySupplyDemand(ArmyData army)
        {
            float demand = 0;
            foreach (var reg in army.regiments)
            {
                demand += reg.currentSize * 0.1f; // 0.1 supply per soldier
            }
            return demand;
        }

        private static void ApplyAttrition(ArmyData army, float excessRatio, Dictionary<FactionType, FactionData> factions)
        {
            // Reduce organization
            float orgLoss = excessRatio * 15f; // Up to 15 org loss per turn
            army.organization = Mathf.Max(0, army.organization - orgLoss);

            // Manpower attrition (soldiers die)
            float attritionRate = excessRatio * 0.02f; // Up to 2% per turn
            foreach (var reg in army.regiments)
            {
                int losses = Mathf.CeilToInt(reg.currentSize * attritionRate);
                reg.currentSize = Mathf.Max(1, reg.currentSize - losses);
            }
        }

        /// <summary>Upgrade infrastructure in a province</summary>
        public static int GetInfrastructureCost(int currentLevel)
        {
            return 200 * currentLevel * currentLevel; // Exponential cost
        }

        public static void UpgradeInfrastructure(string provinceId)
        {
            var data = GetSupplyData(provinceId);
            data.infrastructureLevel = Mathf.Min(10, data.infrastructureLevel + 1);
            data.RecalculateCapacity();
        }

        /// <summary>Get all provinces with supply issues</summary>
        public static List<ProvinceSupplyData> GetCriticalProvinces(float threshold = 0.5f)
        {
            var critical = new List<ProvinceSupplyData>();
            foreach (var kvp in provinceSupply)
            {
                if (kvp.Value.currentDemand > 0 && kvp.Value.SupplyRatio < threshold)
                    critical.Add(kvp.Value);
            }
            return critical;
        }

        /// <summary>Get global supply health (average across occupied provinces)</summary>
        public static float GetGlobalSupplyHealth()
        {
            float totalRatio = 0;
            int count = 0;
            foreach (var kvp in provinceSupply)
            {
                if (kvp.Value.currentDemand > 0)
                {
                    totalRatio += kvp.Value.SupplyRatio;
                    count++;
                }
            }
            return count > 0 ? totalRatio / count : 1f;
        }
    }
}
