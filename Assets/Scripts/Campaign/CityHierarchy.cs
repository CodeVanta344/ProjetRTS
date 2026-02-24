using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Manages a hierarchy of cities: one Capital + 3-4 specialized sub-cities per province.
    /// Inspired by Empire: Total War regional system.
    /// </summary>
    [System.Serializable]
    public class CityHierarchy
    {
        public string provinceId;
        public string provinceName;
        public FactionType owner;
        
        // Main capital city
        public CityNode Capital { get; private set; }
        
        // Sub-cities with specializations
        public List<CityNode> SubCities { get; private set; } = new List<CityNode>();
        
        // Maximum number of sub-cities based on capital level
        public int MaxSubCities => Capital?.cityLevel switch
        {
            1 => 1,  // Village - 1 hamlet
            2 => 2,  // Bourg - 2 villages
            3 => 3,  // Ville - 3 towns
            4 => 4,  // Grande Ville - 4 cities
            5 => 4,  // Métropole - 4 cities max
            _ => 1
        };
        
        public CityHierarchy(string provinceId, string provinceName, FactionType owner)
        {
            this.provinceId = provinceId;
            this.provinceName = provinceName;
            this.owner = owner;
        }
        
        /// <summary>
        /// Initialize the capital city
        /// </summary>
        public void InitializeCapital(CityData capitalCity)
        {
            Capital = new CityNode
            {
                cityId = capitalCity.cityId,
                cityName = capitalCity.cityName,
                cityType = CityType.Capital,
                specialization = CitySpecialization.Administrative,
                population = capitalCity.population,
                cityData = capitalCity,
                position = capitalCity.mapPosition
            };
        }
        
        /// <summary>
        /// Add a sub-city with a specialization
        /// </summary>
        public CityNode AddSubCity(string name, CitySpecialization specialization, Vector2 position)
        {
            if (SubCities.Count >= MaxSubCities)
                return null;
            
            string cityId = $"{provinceId}_sub_{SubCities.Count + 1}";
            
            var node = new CityNode
            {
                cityId = cityId,
                cityName = name,
                cityType = CityType.SubCity,
                specialization = specialization,
                population = Capital?.population / 4 ?? 1000, // Smaller than capital
                position = position,
                cityData = null // Will be created when needed
            };
            
            SubCities.Add(node);
            return node;
        }
        
        /// <summary>
        /// Get total population of the entire region
        /// </summary>
        public int TotalPopulation
        {
            get
            {
                int total = Capital?.population ?? 0;
                foreach (var sub in SubCities)
                    total += sub.population;
                return total;
            }
        }
        
        /// <summary>
        /// Get all resource production bonuses from specializations
        /// </summary>
        public Dictionary<ResourceType, float> GetResourceBonuses()
        {
            var bonuses = new Dictionary<ResourceType, float>();
            
            foreach (var sub in SubCities)
            {
                var (resource, bonus) = GetSpecializationBonus(sub.specialization);
                if (bonuses.ContainsKey(resource))
                    bonuses[resource] += bonus;
                else
                    bonuses[resource] = bonus;
            }
            
            return bonuses;
        }
        
        private (ResourceType resource, float bonus) GetSpecializationBonus(CitySpecialization spec)
        {
            return spec switch
            {
                CitySpecialization.Agriculture => (ResourceType.Food, 0.25f),
                CitySpecialization.Industry => (ResourceType.Iron, 0.20f),
                CitySpecialization.Commerce => (ResourceType.Gold, 0.30f),
                CitySpecialization.Military => (ResourceType.Iron, 0.15f),
                CitySpecialization.Mining => (ResourceType.Iron, 0.35f),
                CitySpecialization.Fishing => (ResourceType.Food, 0.20f),
                CitySpecialization.Forestry => (ResourceType.Wood, 0.30f),
                _ => (ResourceType.Gold, 0f)
            };
        }
        
        /// <summary>
        /// Get display text for the hierarchy
        /// </summary>
        public string GetHierarchyDisplay()
        {
            string text = $"<b>{provinceName}</b>\n";
            
            if (Capital != null)
            {
                text += $"🏛 <color=#FFD700>Capital:</color> {Capital.cityName}\n";
            }
            
            foreach (var sub in SubCities)
            {
                string icon = GetSpecializationIcon(sub.specialization);
                text += $"{icon} {sub.cityName} ({GetSpecializationName(sub.specialization)})\n";
            }
            
            return text;
        }
        
        private string GetSpecializationIcon(CitySpecialization spec)
        {
            return spec switch
            {
                CitySpecialization.Agriculture => "🌾",
                CitySpecialization.Industry => "⚙",
                CitySpecialization.Commerce => "💰",
                CitySpecialization.Military => "⚔",
                CitySpecialization.Mining => "⛏",
                CitySpecialization.Fishing => "🎣",
                CitySpecialization.Forestry => "🌲",
                CitySpecialization.University => "📚",
                CitySpecialization.Port => "⚓",
                _ => "🏘"
            };
        }
        
        private string GetSpecializationName(CitySpecialization spec)
        {
            return spec switch
            {
                CitySpecialization.Agriculture => "Agricultural",
                CitySpecialization.Industry => "Industrial",
                CitySpecialization.Commerce => "Commercial",
                CitySpecialization.Military => "Military",
                CitySpecialization.Mining => "Mining",
                CitySpecialization.Fishing => "Fishing",
                CitySpecialization.Forestry => "Forestry",
                CitySpecialization.University => "University",
                CitySpecialization.Port => "Port",
                CitySpecialization.Administrative => "Administrative",
                _ => "Town"
            };
        }
    }
    
    [System.Serializable]
    public class CityNode
    {
        public string cityId;
        public string cityName;
        public CityType cityType;
        public CitySpecialization specialization;
        public int population;
        public int cityLevel = 1; // Added missing property
        public Vector2 position; // Relative to province center
        public CityData cityData; // Reference to actual city data
        
        // Production modifiers from specialization
        public float GetProductionBonus(ResourceType resource)
        {
            if (cityType != CityType.SubCity) return 0f;
            
            return specialization switch
            {
                CitySpecialization.Agriculture when resource == ResourceType.Food => 0.25f,
                CitySpecialization.Industry when resource == ResourceType.Iron => 0.20f,
                CitySpecialization.Commerce when resource == ResourceType.Gold => 0.30f,
                CitySpecialization.Military when resource == ResourceType.Iron => 0.15f,
                CitySpecialization.Mining when resource == ResourceType.Iron => 0.35f,
                CitySpecialization.Fishing when resource == ResourceType.Food => 0.20f,
                CitySpecialization.Forestry when resource == ResourceType.Wood => 0.30f,
                _ => 0f
            };
        }
    }
    
    public enum CityType
    {
        Capital,    // Main city of the province
        SubCity,    // Smaller specialized settlement
        Port,       // Coastal trade city
        Fortress    // Military stronghold
    }
    
    public enum CitySpecialization
    {
        Administrative,  // Capital - no bonus but required
        Agriculture,     // +Food production
        Industry,        // +Iron +Manufacturing
        Commerce,        // +Gold income
        Military,        // +Recruitment speed +Garrison
        Mining,          // +Iron production
        Fishing,         // +Food (coastal only)
        Forestry,        // +Wood production
        University,      // +Research
        Port             // +Trade income (coastal only)
    }
}
