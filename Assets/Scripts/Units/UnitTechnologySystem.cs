using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Applies technology effects to units in real-time.
    /// Tracks which technologies affect which unit types and applies bonuses accordingly.
    /// </summary>
    public class UnitTechnologySystem : MonoBehaviour
    {
        public static UnitTechnologySystem Instance { get; private set; }
        
        // Current faction's tech tree
        private TechTree techTree;
        private FactionType currentFaction;
        
        // Active technology modifiers per unit type
        private Dictionary<UnitType, UnitTechModifiers> unitModifiers = new Dictionary<UnitType, UnitTechModifiers>();
        
        // Building unlock status
        private Dictionary<BuildingType, bool> unlockedBuildings = new Dictionary<BuildingType, bool>();
        
        [System.Serializable]
        public class UnitTechModifiers
        {
            public float accuracyBonus = 0f;
            public float damageBonus = 0f;
            public float speedBonus = 0f;
            public float moraleBonus = 0f;
            public float reloadSpeedBonus = 0f; // Negative is faster
            public float defenseBonus = 0f;
            public float chargeBonus = 0f;
            public float rangeBonus = 0f;
            
            // Special abilities unlocked
            public bool canFormSquare = false;
            public bool canSkirmish = false;
            public bool canVolleyFire = false;
            public bool hasImprovedFormations = false;
            
            public void Reset()
            {
                accuracyBonus = 0f;
                damageBonus = 0f;
                speedBonus = 0f;
                moraleBonus = 0f;
                reloadSpeedBonus = 0f;
                defenseBonus = 0f;
                chargeBonus = 0f;
                rangeBonus = 0f;
                canFormSquare = false;
                canSkirmish = false;
                canVolleyFire = false;
                hasImprovedFormations = false;
            }
        }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            // Subscribe to tech completion events
            if (CampaignManager.Instance != null)
            {
                var factionData = CampaignManager.Instance.GetPlayerFaction();
                if (factionData != null)
                {
                    techTree = factionData.techTree;
                    currentFaction = CampaignManager.Instance.PlayerFaction;
                }
            }
            
            // Initialize all unit types
            InitializeUnitTypes();
            
            // Apply current technologies
            RecalculateAllModifiers();
        }
        
        /// <summary>
        /// Initialize modifier containers for all unit types
        /// </summary>
        private void InitializeUnitTypes()
        {
            unitModifiers[UnitType.LineInfantry] = new UnitTechModifiers();
            unitModifiers[UnitType.LightInfantry] = new UnitTechModifiers();
            unitModifiers[UnitType.Grenadier] = new UnitTechModifiers();
            unitModifiers[UnitType.Cavalry] = new UnitTechModifiers();
            unitModifiers[UnitType.Lancer] = new UnitTechModifiers();
            unitModifiers[UnitType.Hussar] = new UnitTechModifiers();
            unitModifiers[UnitType.Artillery] = new UnitTechModifiers();
            unitModifiers[UnitType.ImperialGuard] = new UnitTechModifiers();
            unitModifiers[UnitType.GuardCavalry] = new UnitTechModifiers();
            unitModifiers[UnitType.GuardArtillery] = new UnitTechModifiers();
        }
        
        /// <summary>
        /// Recalculate all technology modifiers based on researched technologies
        /// </summary>
        public void RecalculateAllModifiers()
        {
            if (techTree == null) return;
            
            // Reset all modifiers
            foreach (var kvp in unitModifiers)
            {
                kvp.Value.Reset();
            }
            
            // Get all researched technologies
            var researchedTechs = techTree.GetResearchedTechs();
            
            // Apply each technology's effects
            foreach (var tech in researchedTechs)
            {
                ApplyTechnologyEffects(tech);
            }
            
            Debug.Log($"[UnitTechnologySystem] Recalculated modifiers for {researchedTechs.Count} technologies");
        }
        
        /// <summary>
        /// Apply a single technology's effects to appropriate unit types
        /// </summary>
        private void ApplyTechnologyEffects(Technology tech)
        {
            // INFANTRY TECHNOLOGIES
            if (tech.id.Contains("infantry") || tech.id.Contains("musket") || tech.id.Contains("rifle") ||
                tech.id.Contains("bayonet") || tech.id.Contains("drill") || tech.id.Contains("fire") ||
                tech.id.Contains("formation") || tech.id.Contains("volley") || tech.id.Contains("skirmish"))
            {
                ApplyToInfantry(tech);
            }
            
            // CAVALRY TECHNOLOGIES
            if (tech.id.Contains("cavalry") || tech.id.Contains("riding") || tech.id.Contains("charge") ||
                tech.id.Contains("hussar") || tech.id.Contains("lancer") || tech.id.Contains("dragoon") ||
                tech.id.Contains("cuirassier") || tech.id.Contains("carbine"))
            {
                ApplyToCavalry(tech);
            }
            
            // ARTILLERY TECHNOLOGIES
            if (tech.id.Contains("artillery") || tech.id.Contains("cannon") || tech.id.Contains("gun") ||
                tech.id.Contains("shell") || tech.id.Contains("siege") || tech.id.Contains("ballistics") ||
                tech.id.Contains("elevation") || tech.id.Contains("rocket"))
            {
                ApplyToArtillery(tech);
            }
            
            // ELITE GUARD TECHNOLOGIES
            if (tech.id.Contains("guard") || tech.id.Contains("elite") || tech.id.Contains("old_guard"))
            {
                ApplyToGuardUnits(tech);
            }
            
            // BUILDING UNLOCKS
            if (tech.unlocksBuilding)
            {
                unlockedBuildings[tech.unlockedBuilding] = true;
                Debug.Log($"[UnitTechnologySystem] Building unlocked: {tech.unlockedBuilding}");
            }
            
            // GLOBAL BONUSES (applied to all unit types)
            ApplyGlobalBonuses(tech);
        }
        
        private void ApplyToInfantry(Technology tech)
        {
            var line = unitModifiers[UnitType.LineInfantry];
            var light = unitModifiers[UnitType.LightInfantry];
            var grenadier = unitModifiers[UnitType.Grenadier];
            var guard = unitModifiers[UnitType.ImperialGuard];
            
            // Accuracy bonuses
            if (tech.accuracyBonus > 0)
            {
                line.accuracyBonus += tech.accuracyBonus;
                light.accuracyBonus += tech.accuracyBonus * 1.2f; // Light infantry benefits more
                grenadier.accuracyBonus += tech.accuracyBonus;
                guard.accuracyBonus += tech.accuracyBonus * 1.5f; // Guard gets even more
            }
            
            // Damage bonuses
            if (tech.damageBonus > 0)
            {
                // Infantry specific technologies
                if (tech.id.Contains("bayonet"))
                {
                    line.damageBonus += tech.damageBonus;
                    grenadier.damageBonus += tech.damageBonus * 1.3f; // Grenadiers excel at bayonet
                    guard.damageBonus += tech.damageBonus * 1.5f;
                }
                else if (tech.id.Contains("musket") || tech.id.Contains("rifle") || tech.id.Contains("fire"))
                {
                    line.damageBonus += tech.damageBonus;
                    light.damageBonus += tech.damageBonus * 1.3f; // Light infantry benefits more from firearms
                    grenadier.damageBonus += tech.damageBonus;
                    guard.damageBonus += tech.damageBonus * 1.5f;
                }
                else
                {
                    line.damageBonus += tech.damageBonus;
                    light.damageBonus += tech.damageBonus;
                    grenadier.damageBonus += tech.damageBonus;
                    guard.damageBonus += tech.damageBonus * 1.5f;
                }
            }
            
            // Morale bonuses
            if (tech.moraleBonus > 0)
            {
                line.moraleBonus += tech.moraleBonus;
                light.moraleBonus += tech.moraleBonus * 0.8f; // Light infantry less affected by morale techs
                grenadier.moraleBonus += tech.moraleBonus * 1.2f;
                guard.moraleBonus += tech.moraleBonus * 2f; // Guard gets massive morale
            }
            
            // Reload speed (from firing technologies)
            if (tech.id.Contains("volley") || tech.id.Contains("rank") || tech.id.Contains("fire"))
            {
                line.reloadSpeedBonus -= 0.1f; // 10% faster
                grenadier.reloadSpeedBonus -= 0.15f; // 15% faster
                guard.reloadSpeedBonus -= 0.2f; // 20% faster
            }
            
            // Range bonuses
            if (tech.id.Contains("rifle") || tech.id.Contains("range"))
            {
                light.rangeBonus += 0.2f; // Light infantry benefits most
                line.rangeBonus += 0.1f;
                guard.rangeBonus += 0.15f;
            }
            
            // Formation abilities
            if (tech.id.Contains("square"))
            {
                line.canFormSquare = true;
                grenadier.canFormSquare = true;
                guard.canFormSquare = true;
            }
            
            if (tech.id.Contains("skirmish"))
            {
                light.canSkirmish = true;
            }
            
            if (tech.id.Contains("volley"))
            {
                line.canVolleyFire = true;
                grenadier.canVolleyFire = true;
                guard.canVolleyFire = true;
            }
        }
        
        private void ApplyToCavalry(Technology tech)
        {
            var cav = unitModifiers[UnitType.Cavalry];
            var hussar = unitModifiers[UnitType.Hussar];
            var lancer = unitModifiers[UnitType.Lancer];
            var guardCav = unitModifiers[UnitType.GuardCavalry];
            
            // Speed bonuses
            if (tech.speedBonus > 0)
            {
                cav.speedBonus += tech.speedBonus;
                hussar.speedBonus += tech.speedBonus * 1.5f; // Hussars are fastest
                lancer.speedBonus += tech.speedBonus;
                guardCav.speedBonus += tech.speedBonus * 1.3f;
            }
            
            // Damage bonuses
            if (tech.damageBonus > 0)
            {
                cav.damageBonus += tech.damageBonus;
                hussar.damageBonus += tech.damageBonus * 0.9f; // Hussars less charge damage
                lancer.damageBonus += tech.damageBonus * 1.3f; // Lancers excel at charge
                guardCav.damageBonus += tech.damageBonus * 1.5f;
            }
            
            // Charge bonuses
            if (tech.id.Contains("charge") || tech.id.Contains("shock"))
            {
                cav.chargeBonus += 0.15f;
                lancer.chargeBonus += 0.25f; // Lancers excel
                guardCav.chargeBonus += 0.3f;
            }
            
            // Accuracy (for mounted firearms)
            if (tech.accuracyBonus > 0 && tech.id.Contains("carbine"))
            {
                hussar.accuracyBonus += tech.accuracyBonus * 1.5f; // Hussars use carbines
                cav.accuracyBonus += tech.accuracyBonus;
            }
            
            // Morale
            if (tech.moraleBonus > 0)
            {
                cav.moraleBonus += tech.moraleBonus;
                hussar.moraleBonus += tech.moraleBonus * 0.9f;
                lancer.moraleBonus += tech.moraleBonus * 1.1f;
                guardCav.moraleBonus += tech.moraleBonus * 1.5f;
            }
        }
        
        private void ApplyToArtillery(Technology tech)
        {
            var art = unitModifiers[UnitType.Artillery];
            var guardArt = unitModifiers[UnitType.GuardArtillery];
            
            // Damage bonuses
            if (tech.damageBonus > 0)
            {
                art.damageBonus += tech.damageBonus;
                guardArt.damageBonus += tech.damageBonus * 1.5f;
            }
            
            // Accuracy bonuses
            if (tech.accuracyBonus > 0)
            {
                art.accuracyBonus += tech.accuracyBonus;
                guardArt.accuracyBonus += tech.accuracyBonus * 1.5f;
            }
            
            // Speed (mobility)
            if (tech.speedBonus > 0 || tech.id.Contains("horse"))
            {
                art.speedBonus += tech.speedBonus;
                guardArt.speedBonus += tech.speedBonus * 1.3f;
            }
            
            // Range bonuses
            if (tech.id.Contains("rifled") || tech.id.Contains("elevation"))
            {
                art.rangeBonus += 0.15f;
                guardArt.rangeBonus += 0.25f;
            }
            
            // Reload speed
            if (tech.id.Contains("recoil") || tech.id.Contains("system"))
            {
                art.reloadSpeedBonus -= 0.2f; // 20% faster
                guardArt.reloadSpeedBonus -= 0.3f; // 30% faster
            }
            
            // Morale
            if (tech.moraleBonus > 0)
            {
                art.moraleBonus += tech.moraleBonus * 0.5f; // Artillery less affected
                guardArt.moraleBonus += tech.moraleBonus;
            }
        }
        
        private void ApplyToGuardUnits(Technology tech)
        {
            // Guard units get additional bonuses on top of base unit bonuses
            var guard = unitModifiers[UnitType.ImperialGuard];
            var guardCav = unitModifiers[UnitType.GuardCavalry];
            var guardArt = unitModifiers[UnitType.GuardArtillery];
            
            // Old Guard specific
            if (tech.id.Contains("old_guard") || tech.id.Contains("guard"))
            {
                guard.accuracyBonus += 0.1f;
                guard.damageBonus += 0.15f;
                guard.moraleBonus += 30f; // Unbreakable morale
                guard.defenseBonus += 0.2f;
                
                guardCav.chargeBonus += 0.2f;
                guardCav.damageBonus += 0.15f;
                guardCav.moraleBonus += 25f;
                
                guardArt.damageBonus += 0.2f;
                guardArt.accuracyBonus += 0.15f;
                guardArt.moraleBonus += 20f;
            }
        }
        
        private void ApplyGlobalBonuses(Technology tech)
        {
            // Apply to ALL unit types
            foreach (var kvp in unitModifiers)
            {
                var mod = kvp.Value;
                
                mod.accuracyBonus += tech.accuracyBonus * 0.5f; // 50% of infantry bonus
                mod.damageBonus += tech.damageBonus * 0.3f; // 30% global
                mod.speedBonus += tech.speedBonus;
                mod.moraleBonus += tech.moraleBonus;
            }
        }
        
        /// <summary>
        /// Get the technology modifiers for a specific unit type
        /// </summary>
        public UnitTechModifiers GetModifiersForUnit(UnitType type)
        {
            if (unitModifiers.ContainsKey(type))
                return unitModifiers[type];
            
            return new UnitTechModifiers(); // Return empty if not found
        }
        
        /// <summary>
        /// Check if a building type is unlocked by technology
        /// </summary>
        public bool IsBuildingUnlocked(BuildingType type)
        {
            return unlockedBuildings.ContainsKey(type) && unlockedBuildings[type];
        }
        
        /// <summary>
        /// Called when a new technology is researched - updates modifiers
        /// </summary>
        public void OnTechnologyResearched(Technology tech)
        {
            ApplyTechnologyEffects(tech);
            
            // Update all existing units in the scene
            UpdateAllActiveUnits();
            
            Debug.Log($"[UnitTechnologySystem] Applied effects from: {tech.name}");
        }
        
        /// <summary>
        /// Update all active units with current technology modifiers
        /// </summary>
        private void UpdateAllActiveUnits()
        {
            var allUnits = FindObjectsByType<UnitBase>(FindObjectsSortMode.None);
            foreach (var unit in allUnits)
            {
                if (unit.Data != null)
                {
                    unit.ApplyTechnologyModifiers();
                }
            }
        }
        
        /// <summary>
        /// Get display text for unit technology status
        /// </summary>
        public string GetUnitTechStatus(UnitType type)
        {
            var mod = GetModifiersForUnit(type);
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Technology Bonuses:");
            
            if (mod.accuracyBonus > 0) sb.AppendLine($"  Accuracy: +{(mod.accuracyBonus * 100):F0}%");
            if (mod.damageBonus > 0) sb.AppendLine($"  Damage: +{(mod.damageBonus * 100):F0}%");
            if (mod.speedBonus > 0) sb.AppendLine($"  Speed: +{(mod.speedBonus * 100):F0}%");
            if (mod.moraleBonus > 0) sb.AppendLine($"  Morale: +{mod.moraleBonus:F0}");
            if (mod.rangeBonus > 0) sb.AppendLine($"  Range: +{(mod.rangeBonus * 100):F0}%");
            if (mod.reloadSpeedBonus < 0) sb.AppendLine($"  Reload: {(mod.reloadSpeedBonus * 100):F0}% faster");
            if (mod.chargeBonus > 0) sb.AppendLine($"  Charge: +{(mod.chargeBonus * 100):F0}%");
            if (mod.defenseBonus > 0) sb.AppendLine($"  Defense: +{(mod.defenseBonus * 100):F0}%");
            
            if (mod.canFormSquare) sb.AppendLine("  ✓ Can form square");
            if (mod.canSkirmish) sb.AppendLine("  ✓ Can skirmish");
            if (mod.canVolleyFire) sb.AppendLine("  ✓ Can volley fire");
            
            return sb.ToString();
        }
    }
}
