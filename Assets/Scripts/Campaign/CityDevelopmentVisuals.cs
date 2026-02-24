using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Attached to each city marker GameObject. Tracks visual state and triggers
    /// rebuild only when underlying data changes (level up, building completed/upgraded, tech researched).
    /// Also refreshes the linked CityNameLabel with building icons.
    /// </summary>
    public class CityDevelopmentVisuals : MonoBehaviour
    {
        // Data references
        public CityData CityData { get; private set; }
        public FactionType Faction { get; private set; }
        public bool IsNationCapital { get; private set; }

        // Linked label for building icon updates
        public CityNameLabel LinkedLabel { get; set; }

        // Cached visual state (last rendered)
        private int lastCityLevel;
        private int lastBuildingCount;
        private int lastConstructingCount;
        private int lastTotalBuildingLevels;
        private int lastTechEra;
        private FactionType lastFaction;

        // Child containers
        private GameObject visualRoot;

        public void Initialize(CityData cityData, FactionType faction, bool isNationCapital, int techEra)
        {
            CityData = cityData;
            Faction = faction;
            IsNationCapital = isNationCapital;

            lastCityLevel = cityData?.cityLevel ?? 1;
            lastBuildingCount = CountConstructedBuildings();
            lastConstructingCount = CountConstructingBuildings();
            lastTotalBuildingLevels = SumBuildingLevels();
            lastTechEra = techEra;
            lastFaction = faction;

            RebuildVisuals(techEra);
        }

        /// <summary>
        /// Check if the underlying data has changed enough to warrant a visual rebuild.
        /// Now also tracks building level upgrades (not just count changes).
        /// </summary>
        public bool NeedsVisualUpdate(int currentTechEra)
        {
            if (CityData == null) return false;

            if (CityData.cityLevel != lastCityLevel) return true;
            if (CountConstructedBuildings() != lastBuildingCount) return true;
            if (CountConstructingBuildings() != lastConstructingCount) return true;
            if (SumBuildingLevels() != lastTotalBuildingLevels) return true;
            if (currentTechEra != lastTechEra) return true;
            if (CityData.owner != lastFaction) return true;

            return false;
        }

        /// <summary>
        /// Destroy existing visuals and regenerate from current data.
        /// Also refreshes the linked CityNameLabel.
        /// </summary>
        public void RebuildVisuals(int techEra)
        {
            // Destroy old visual root
            if (visualRoot != null)
                Destroy(visualRoot);

            // Update cached state
            lastCityLevel = CityData?.cityLevel ?? 1;
            lastBuildingCount = CountConstructedBuildings();
            lastConstructingCount = CountConstructingBuildings();
            lastTotalBuildingLevels = SumBuildingLevels();
            lastTechEra = techEra;
            lastFaction = CityData?.owner ?? Faction;
            Faction = lastFaction;

            // Generate new visuals
            visualRoot = CityVisualGenerator.CreateCity(CityData, Faction, techEra, IsNationCapital, transform);

            // Refresh linked label with building data
            RefreshLabel();
        }

        /// <summary>
        /// Conditionally rebuild if data changed.
        /// Returns true if rebuild happened.
        /// </summary>
        public bool TryRefresh(int currentTechEra)
        {
            if (!NeedsVisualUpdate(currentTechEra)) return false;
            RebuildVisuals(currentTechEra);
            return true;
        }

        /// <summary>
        /// Refresh only the label (without rebuilding 3D visuals).
        /// Called externally when label needs updating without full visual rebuild.
        /// </summary>
        public void RefreshLabel()
        {
            if (LinkedLabel != null && CityData != null)
                LinkedLabel.RefreshFromCityData(CityData);
        }

        private int CountConstructedBuildings()
        {
            if (CityData?.buildings == null) return 0;
            int count = 0;
            foreach (var b in CityData.buildings)
                if (b.isConstructed) count++;
            return count;
        }

        private int CountConstructingBuildings()
        {
            if (CityData?.buildings == null) return 0;
            int count = 0;
            foreach (var b in CityData.buildings)
                if (b.isConstructing && !b.isConstructed) count++;
            return count;
        }

        /// <summary>
        /// Sum all building levels so we detect upgrades (Lv1->Lv2 etc.)
        /// even when count doesn't change.
        /// </summary>
        private int SumBuildingLevels()
        {
            if (CityData?.buildings == null) return 0;
            int sum = 0;
            foreach (var b in CityData.buildings)
                sum += b.level;
            return sum;
        }
    }
}
