using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// HoI4-style Division Template. Defines the composition of a division 
    /// (how many of each battalion type) and auto-calculates stats.
    /// </summary>
    
    public enum BattalionType
    {
        Infantry,       // Standard line infantry
        LightInfantry,  // Skirmishers, faster but weaker
        Grenadier,      // Elite heavy infantry
        Artillery,      // Field guns
        HeavyArtillery, // Siege/heavy guns
        Cavalry,        // Standard cavalry
        Hussar,         // Light cavalry, recon
        Lancer,         // Shock cavalry
        Guard           // Imperial/Royal Guard, elite
    }

    [System.Serializable]
    public class BattalionSlot
    {
        public BattalionType type;
        public int count = 1;

        public BattalionSlot(BattalionType type, int count = 1)
        {
            this.type = type;
            this.count = count;
        }
    }

    [System.Serializable]
    public class DivisionTemplate
    {
        public string templateId;
        public string templateName;
        public FactionType faction;
        public List<BattalionSlot> battalions = new List<BattalionSlot>();

        // === COMPUTED STATS ===
        public float attack;
        public float defense;
        public float breakthrough;
        public int combatWidth;
        public float organization;
        public float speed;         // km/h
        public int manpowerCost;

        public DivisionTemplate(string name, FactionType faction)
        {
            templateId = System.Guid.NewGuid().ToString().Substring(0, 8);
            templateName = name;
            this.faction = faction;
        }

        public void AddBattalion(BattalionType type, int count = 1)
        {
            var existing = battalions.Find(b => b.type == type);
            if (existing != null)
                existing.count += count;
            else
                battalions.Add(new BattalionSlot(type, count));
            RecalculateStats();
        }

        public void RemoveBattalion(BattalionType type, int count = 1)
        {
            var existing = battalions.Find(b => b.type == type);
            if (existing != null)
            {
                existing.count -= count;
                if (existing.count <= 0)
                    battalions.Remove(existing);
            }
            RecalculateStats();
        }

        public int TotalBattalions
        {
            get
            {
                int total = 0;
                foreach (var b in battalions) total += b.count;
                return total;
            }
        }

        public void RecalculateStats()
        {
            attack = 0; defense = 0; breakthrough = 0;
            combatWidth = 0; organization = 0; speed = 999f;
            manpowerCost = 0;

            int totalBattalions = 0;
            foreach (var slot in battalions)
            {
                var stats = GetBattalionStats(slot.type);
                attack += stats.attack * slot.count;
                defense += stats.defense * slot.count;
                breakthrough += stats.breakthrough * slot.count;
                combatWidth += stats.combatWidth * slot.count;
                organization += stats.organization * slot.count;
                speed = Mathf.Min(speed, stats.speed);
                manpowerCost += stats.manpower * slot.count;
                totalBattalions += slot.count;
            }

            if (totalBattalions > 0)
                organization /= totalBattalions; // Average org
            else
                speed = 0;
        }

        /// <summary>Get equipment requirements for this template</summary>
        public Dictionary<EquipmentType, int> GetEquipmentRequirements()
        {
            var reqs = new Dictionary<EquipmentType, int>();
            foreach (var slot in battalions)
            {
                var slotReqs = GetBattalionEquipment(slot.type);
                foreach (var kvp in slotReqs)
                {
                    if (!reqs.ContainsKey(kvp.Key)) reqs[kvp.Key] = 0;
                    reqs[kvp.Key] += kvp.Value * slot.count;
                }
            }
            return reqs;
        }

        public DivisionTemplate Clone()
        {
            var clone = new DivisionTemplate(templateName + " (copie)", faction);
            foreach (var b in battalions)
                clone.battalions.Add(new BattalionSlot(b.type, b.count));
            clone.RecalculateStats();
            return clone;
        }

        // === BATTALION STATS ===
        public struct BattalionStats
        {
            public float attack, defense, breakthrough, organization, speed;
            public int combatWidth, manpower;
        }

        public static BattalionStats GetBattalionStats(BattalionType type) => type switch
        {
            BattalionType.Infantry =>       new BattalionStats { attack = 6, defense = 8, breakthrough = 2, combatWidth = 2, organization = 60, speed = 4, manpower = 1000 },
            BattalionType.LightInfantry =>  new BattalionStats { attack = 5, defense = 5, breakthrough = 3, combatWidth = 1, organization = 55, speed = 5, manpower = 800 },
            BattalionType.Grenadier =>      new BattalionStats { attack = 10, defense = 10, breakthrough = 5, combatWidth = 2, organization = 70, speed = 4, manpower = 800 },
            BattalionType.Artillery =>      new BattalionStats { attack = 12, defense = 2, breakthrough = 8, combatWidth = 3, organization = 30, speed = 3, manpower = 500 },
            BattalionType.HeavyArtillery => new BattalionStats { attack = 18, defense = 1, breakthrough = 14, combatWidth = 4, organization = 20, speed = 2, manpower = 600 },
            BattalionType.Cavalry =>        new BattalionStats { attack = 8, defense = 4, breakthrough = 6, combatWidth = 2, organization = 50, speed = 8, manpower = 600 },
            BattalionType.Hussar =>         new BattalionStats { attack = 6, defense = 3, breakthrough = 8, combatWidth = 2, organization = 45, speed = 10, manpower = 500 },
            BattalionType.Lancer =>         new BattalionStats { attack = 10, defense = 3, breakthrough = 10, combatWidth = 2, organization = 45, speed = 9, manpower = 500 },
            BattalionType.Guard =>          new BattalionStats { attack = 14, defense = 12, breakthrough = 8, combatWidth = 2, organization = 80, speed = 4, manpower = 600 },
            _ =>                            new BattalionStats { attack = 5, defense = 5, breakthrough = 2, combatWidth = 2, organization = 50, speed = 4, manpower = 1000 }
        };

        public static Dictionary<EquipmentType, int> GetBattalionEquipment(BattalionType type)
        {
            var eq = new Dictionary<EquipmentType, int>();
            switch (type)
            {
                case BattalionType.Infantry:
                case BattalionType.LightInfantry:
                case BattalionType.Grenadier:
                case BattalionType.Guard:
                    eq[EquipmentType.Muskets] = 1000;
                    eq[EquipmentType.Bayonets] = 1000;
                    eq[EquipmentType.Uniforms] = 1000;
                    eq[EquipmentType.Gunpowder] = 500;
                    break;
                case BattalionType.Artillery:
                    eq[EquipmentType.CannonsLight] = 12;
                    eq[EquipmentType.Gunpowder] = 400;
                    eq[EquipmentType.Cannonballs] = 600;
                    eq[EquipmentType.Horses] = 100;
                    break;
                case BattalionType.HeavyArtillery:
                    eq[EquipmentType.CannonsHeavy] = 8;
                    eq[EquipmentType.CannonsSiege] = 4;
                    eq[EquipmentType.Gunpowder] = 600;
                    eq[EquipmentType.Cannonballs] = 1000;
                    eq[EquipmentType.Horses] = 200;
                    break;
                case BattalionType.Cavalry:
                case BattalionType.Hussar:
                case BattalionType.Lancer:
                    eq[EquipmentType.Sabres] = 500;
                    eq[EquipmentType.Horses] = 600;
                    eq[EquipmentType.Uniforms] = 500;
                    break;
            }
            return eq;
        }

        public static string GetBattalionDisplayName(BattalionType type) => type switch
        {
            BattalionType.Infantry => "Infanterie",
            BattalionType.LightInfantry => "Inf. Légère",
            BattalionType.Grenadier => "Grenadiers",
            BattalionType.Artillery => "Artillerie",
            BattalionType.HeavyArtillery => "Art. Lourde",
            BattalionType.Cavalry => "Cavalerie",
            BattalionType.Hussar => "Hussards",
            BattalionType.Lancer => "Lanciers",
            BattalionType.Guard => "Garde Imp.",
            _ => type.ToString()
        };

        public static string GetBattalionIcon(BattalionType type) => type switch
        {
            BattalionType.Infantry => "INF",
            BattalionType.LightInfantry => "LGT",
            BattalionType.Grenadier => "GRD",
            BattalionType.Artillery => "ART",
            BattalionType.HeavyArtillery => "H.A",
            BattalionType.Cavalry => "CAV",
            BattalionType.Hussar => "HUS",
            BattalionType.Lancer => "LAN",
            BattalionType.Guard => "GDE",
            _ => "???"
        };
    }

    /// <summary>
    /// Manages all division templates for all factions.
    /// </summary>
    public static class DivisionDesigner
    {
        private static Dictionary<FactionType, List<DivisionTemplate>> templates = 
            new Dictionary<FactionType, List<DivisionTemplate>>();

        public static void Initialize(FactionType faction)
        {
            if (!templates.ContainsKey(faction))
                templates[faction] = new List<DivisionTemplate>();
        }

        public static List<DivisionTemplate> GetTemplates(FactionType faction)
        {
            Initialize(faction);
            return templates[faction];
        }

        public static DivisionTemplate CreateTemplate(string name, FactionType faction)
        {
            Initialize(faction);
            var t = new DivisionTemplate(name, faction);
            templates[faction].Add(t);
            return t;
        }

        public static void DeleteTemplate(FactionType faction, string templateId)
        {
            Initialize(faction);
            templates[faction].RemoveAll(t => t.templateId == templateId);
        }

        public static DivisionTemplate DuplicateTemplate(FactionType faction, string templateId)
        {
            Initialize(faction);
            var original = templates[faction].Find(t => t.templateId == templateId);
            if (original == null) return null;
            var clone = original.Clone();
            templates[faction].Add(clone);
            return clone;
        }

        /// <summary>Create default templates for a faction</summary>
        public static void CreateDefaultTemplates(FactionType faction)
        {
            Initialize(faction);
            
            // Standard infantry division (6 INF + 2 ART)
            var infDiv = CreateTemplate("Division d'infanterie standard", faction);
            infDiv.AddBattalion(BattalionType.Infantry, 6);
            infDiv.AddBattalion(BattalionType.Artillery, 2);

            // Cavalry division (4 CAV + 2 HUS)
            var cavDiv = CreateTemplate("Division de cavalerie", faction);
            cavDiv.AddBattalion(BattalionType.Cavalry, 4);
            cavDiv.AddBattalion(BattalionType.Hussar, 2);

            // Artillery division (2 ART + 2 H.ART + 2 INF)
            var artDiv = CreateTemplate("Division d'artillerie", faction);
            artDiv.AddBattalion(BattalionType.Artillery, 2);
            artDiv.AddBattalion(BattalionType.HeavyArtillery, 2);
            artDiv.AddBattalion(BattalionType.Infantry, 2);
        }
    }
}
