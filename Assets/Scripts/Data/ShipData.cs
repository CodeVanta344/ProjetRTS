using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Naval
{
    public enum ShipType
    {
        Sloop,          // Small, fast, 8-14 guns
        Brig,           // Light warship, 10-18 guns
        Frigate,        // Medium warship, 32-44 guns
        ShipOfTheLine,  // Heavy warship, 74-120 guns
        Fireship,       // Explosive ship, no guns
        Transport       // Troop transport, minimal guns
    }

    public enum ShipSection
    {
        Hull,
        Sails,
        Crew,
        Cannons
    }

    public enum AmmoType
    {
        RoundShot,      // Standard - hull damage
        ChainShot,      // Sail/rigging damage
        Grapeshot       // Crew damage
    }

    [CreateAssetMenu(fileName = "NewShipData", menuName = "Napoleonic Wars/Ship Data")]
    public class ShipData : ScriptableObject
    {
        [Header("Identity")]
        public string shipName = "HMS Victory";
        public ShipType shipType = ShipType.ShipOfTheLine;
        public FactionType faction = FactionType.Britain;

        [Header("Armament")]
        public int cannonCount = 74;
        public int cannonsPerBroadside => cannonCount / 2;
        public float cannonDamage = 25f;
        public float cannonRange = 300f;
        public float reloadTime = 8f;

        [Header("Hull")]
        public float maxHullHP = 1000f;
        public float armor = 50f; // Damage reduction

        [Header("Sails & Speed")]
        public float maxSailHP = 500f;
        public float maxSpeed = 8f; // Knots
        public float turnRate = 15f; // Degrees per second
        public float acceleration = 2f;

        [Header("Crew")]
        public int maxCrew = 600;
        public float crewMorale = 100f;
        public int marinesCount = 50; // For boarding

        [Header("Cargo")]
        public int cargoCapacity = 100;
        public int troopCapacity = 0; // For transports

        [Header("Costs")]
        public int buildCost = 5000;
        public int turnsToBuild = 5;
        public float maintenanceCost = 100f;

        [Header("Visuals")]
        public GameObject shipPrefab;
        public float shipScale = 1f;

        public static ShipData CreateDefault(ShipType type, FactionType faction)
        {
            ShipData data = CreateInstance<ShipData>();
            data.shipType = type;
            data.faction = faction;

            switch (type)
            {
                case ShipType.Sloop:
                    data.shipName = "Sloop";
                    data.cannonCount = 12;
                    data.maxHullHP = 200f;
                    data.maxSailHP = 150f;
                    data.maxSpeed = 12f;
                    data.turnRate = 25f;
                    data.maxCrew = 75;
                    data.marinesCount = 10;
                    data.armor = 10f;
                    data.buildCost = 800;
                    data.turnsToBuild = 2;
                    data.maintenanceCost = 20f;
                    break;

                case ShipType.Brig:
                    data.shipName = "Brig";
                    data.cannonCount = 16;
                    data.maxHullHP = 350f;
                    data.maxSailHP = 200f;
                    data.maxSpeed = 10f;
                    data.turnRate = 20f;
                    data.maxCrew = 120;
                    data.marinesCount = 20;
                    data.armor = 20f;
                    data.buildCost = 1500;
                    data.turnsToBuild = 3;
                    data.maintenanceCost = 40f;
                    break;

                case ShipType.Frigate:
                    data.shipName = "Frigate";
                    data.cannonCount = 38;
                    data.maxHullHP = 600f;
                    data.maxSailHP = 350f;
                    data.maxSpeed = 9f;
                    data.turnRate = 18f;
                    data.maxCrew = 300;
                    data.marinesCount = 40;
                    data.armor = 35f;
                    data.buildCost = 3000;
                    data.turnsToBuild = 4;
                    data.maintenanceCost = 80f;
                    break;

                case ShipType.ShipOfTheLine:
                    data.shipName = "Ship of the Line";
                    data.cannonCount = 74;
                    data.maxHullHP = 1200f;
                    data.maxSailHP = 500f;
                    data.maxSpeed = 7f;
                    data.turnRate = 12f;
                    data.maxCrew = 650;
                    data.marinesCount = 80;
                    data.armor = 60f;
                    data.buildCost = 6000;
                    data.turnsToBuild = 6;
                    data.maintenanceCost = 150f;
                    break;

                case ShipType.Fireship:
                    data.shipName = "Fireship";
                    data.cannonCount = 0;
                    data.maxHullHP = 100f;
                    data.maxSailHP = 100f;
                    data.maxSpeed = 8f;
                    data.turnRate = 20f;
                    data.maxCrew = 20;
                    data.marinesCount = 0;
                    data.armor = 0f;
                    data.buildCost = 500;
                    data.turnsToBuild = 1;
                    data.maintenanceCost = 10f;
                    break;

                case ShipType.Transport:
                    data.shipName = "Transport";
                    data.cannonCount = 8;
                    data.maxHullHP = 400f;
                    data.maxSailHP = 300f;
                    data.maxSpeed = 6f;
                    data.turnRate = 15f;
                    data.maxCrew = 50;
                    data.marinesCount = 0;
                    data.troopCapacity = 500;
                    data.armor = 15f;
                    data.buildCost = 2000;
                    data.turnsToBuild = 3;
                    data.maintenanceCost = 50f;
                    break;
            }

            return data;
        }
    }
}
