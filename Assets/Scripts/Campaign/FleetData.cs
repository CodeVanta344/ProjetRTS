using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Naval;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Fleet data for the campaign map. Similar to ArmyData but for naval forces.
    /// </summary>
    [System.Serializable]
    public class FleetData
    {
        public string fleetId;
        public string fleetName;
        public FactionType faction;
        public string currentPortId;      // Docked at port
        public Vector2 seaPosition;       // Position at sea
        public bool isAtSea;
        public int movementPoints = 3;
        public int maxMovementPoints = 3;

        public List<ShipInfo> ships = new List<ShipInfo>();

        // Blockade state
        public bool isBlockading;
        public string blockadedPortId;

        // Transport
        public List<string> embarkedArmyIds = new List<string>();

        public int TotalCannons
        {
            get
            {
                int total = 0;
                foreach (var ship in ships) total += ship.cannonCount;
                return total;
            }
        }

        public int TotalCrew
        {
            get
            {
                int total = 0;
                foreach (var ship in ships) total += ship.currentCrew;
                return total;
            }
        }

        public float MaintenanceCost
        {
            get
            {
                float cost = 0f;
                foreach (var ship in ships) cost += ship.maintenanceCost;
                return cost;
            }
        }

        public int ShipCount => ships.Count;

        public FleetData(string name, string id, FactionType faction, string portId)
        {
            fleetName = name;
            fleetId = id;
            this.faction = faction;
            currentPortId = portId;
            isAtSea = false;
        }

        public void AddShip(ShipInfo ship)
        {
            ships.Add(ship);
        }

        public void RemoveShip(ShipInfo ship)
        {
            ships.Remove(ship);
        }

        public void ResetMovement()
        {
            movementPoints = maxMovementPoints;
        }

        public void SetToSea(Vector2 position)
        {
            isAtSea = true;
            seaPosition = position;
            currentPortId = null;
        }

        public void DockAtPort(string portId)
        {
            isAtSea = false;
            currentPortId = portId;
            isBlockading = false;
            blockadedPortId = null;
        }

        public void StartBlockade(string portId)
        {
            isBlockading = true;
            blockadedPortId = portId;
        }

        public void EndBlockade()
        {
            isBlockading = false;
            blockadedPortId = null;
        }
    }

    /// <summary>
    /// Individual ship info for campaign
    /// </summary>
    [System.Serializable]
    public class ShipInfo
    {
        public string shipName;
        public ShipType shipType;
        public int cannonCount;
        public int maxCrew;
        public int currentCrew;
        public float hullHP;
        public float maxHullHP;
        public float maintenanceCost;
        public float experience;

        public ShipInfo(string name, ShipType type)
        {
            shipName = name;
            shipType = type;

            // Set defaults based on type
            var defaultData = ShipData.CreateDefault(type, FactionType.Britain);
            cannonCount = defaultData.cannonCount;
            maxCrew = defaultData.maxCrew;
            currentCrew = maxCrew;
            maxHullHP = defaultData.maxHullHP;
            hullHP = maxHullHP;
            maintenanceCost = defaultData.maintenanceCost;
        }

        public void Repair(float amount)
        {
            hullHP = Mathf.Min(maxHullHP, hullHP + amount);
        }

        public void RecrewTo(int crew)
        {
            currentCrew = Mathf.Min(maxCrew, crew);
        }
    }

    /// <summary>
    /// Port data for naval operations
    /// </summary>
    [System.Serializable]
    public class PortData
    {
        public string portId;
        public string portName;
        public string provinceId;
        public FactionType owner;
        public Vector2 mapPosition;

        // Port capabilities
        public int dockCapacity = 5;      // Max ships that can dock
        public int shipyardLevel = 1;     // 0 = no shipyard, 1-3 = can build ships
        public bool canRepair = true;
        public bool canRecruit = true;

        // Construction queue
        public List<ShipConstruction> constructionQueue = new List<ShipConstruction>();

        // Blockade state
        public bool isBlockaded;
        public string blockadingFleetId;

        public PortData(string id, string name, string provinceId, FactionType owner, Vector2 position)
        {
            this.portId = id;
            this.portName = name;
            this.provinceId = provinceId;
            this.owner = owner;
            this.mapPosition = position;
        }

        public bool CanBuildShip(ShipType type)
        {
            if (shipyardLevel == 0) return false;
            if (isBlockaded) return false;

            switch (type)
            {
                case ShipType.Sloop:
                case ShipType.Fireship:
                    return shipyardLevel >= 1;
                case ShipType.Brig:
                case ShipType.Transport:
                    return shipyardLevel >= 1;
                case ShipType.Frigate:
                    return shipyardLevel >= 2;
                case ShipType.ShipOfTheLine:
                    return shipyardLevel >= 3;
                default:
                    return false;
            }
        }

        public void StartConstruction(ShipType type, string shipName)
        {
            var defaultData = ShipData.CreateDefault(type, owner);
            constructionQueue.Add(new ShipConstruction
            {
                shipName = shipName,
                shipType = type,
                turnsRemaining = defaultData.turnsToBuild
            });
        }

        public ShipInfo ProcessConstruction()
        {
            if (constructionQueue.Count == 0) return null;

            var construction = constructionQueue[0];
            construction.turnsRemaining--;

            if (construction.turnsRemaining <= 0)
            {
                constructionQueue.RemoveAt(0);
                return new ShipInfo(construction.shipName, construction.shipType);
            }

            return null;
        }
    }

    [System.Serializable]
    public class ShipConstruction
    {
        public string shipName;
        public ShipType shipType;
        public int turnsRemaining;
    }

    /// <summary>
    /// Manages fleets and ports on the campaign map
    /// </summary>
    public class NavalCampaignManager
    {
        private static NavalCampaignManager _instance;
        public static NavalCampaignManager Instance => _instance ??= new NavalCampaignManager();

        private Dictionary<string, FleetData> fleets = new Dictionary<string, FleetData>();
        private Dictionary<string, PortData> ports = new Dictionary<string, PortData>();

        public Dictionary<string, FleetData> Fleets => fleets;
        public Dictionary<string, PortData> Ports => ports;

        public void Initialize()
        {
            CreatePorts();
            CreateStartingFleets();
        }

        private void CreatePorts()
        {
            // British ports
            AddPort("portsmouth", "Portsmouth", "london", FactionType.Britain, new Vector2(0.27f, 0.70f), 3);
            AddPort("gibraltar_port", "Gibraltar", "gibraltar", FactionType.Britain, new Vector2(0.05f, 0.14f), 2);

            // French ports
            AddPort("brest", "Brest", "brittany", FactionType.France, new Vector2(0.15f, 0.60f), 3);
            AddPort("toulon", "Toulon", "marseille", FactionType.France, new Vector2(0.35f, 0.30f), 2);

            // Spanish ports
            AddPort("cadiz", "Cadiz", "andalusia", FactionType.Spain, new Vector2(0.06f, 0.20f), 3);
            AddPort("barcelona", "Barcelona", "aragon", FactionType.Spain, new Vector2(0.22f, 0.25f), 2);

            // Ottoman ports
            AddPort("istanbul_port", "Istanbul", "constantinople", FactionType.Ottoman, new Vector2(0.70f, 0.32f), 2);
            AddPort("alexandria", "Alexandria", "egypt", FactionType.Ottoman, new Vector2(0.75f, 0.10f), 2);

            // Russian ports
            AddPort("sevastopol", "Sevastopol", "smolensk", FactionType.Russia, new Vector2(0.68f, 0.45f), 1);

            Debug.Log($"[Naval] Created {ports.Count} ports");
        }

        private void AddPort(string id, string name, string provinceId, FactionType owner, Vector2 pos, int shipyardLevel)
        {
            var port = new PortData(id, name, provinceId, owner, pos);
            port.shipyardLevel = shipyardLevel;
            ports[id] = port;
        }

        private void CreateStartingFleets()
        {
            // British Royal Navy
            FleetData britishFleet = new FleetData("Royal Navy", "fleet_britain", FactionType.Britain, "portsmouth");
            britishFleet.AddShip(new ShipInfo("HMS Victory", ShipType.ShipOfTheLine));
            britishFleet.AddShip(new ShipInfo("HMS Temeraire", ShipType.ShipOfTheLine));
            britishFleet.AddShip(new ShipInfo("HMS Neptune", ShipType.ShipOfTheLine));
            britishFleet.AddShip(new ShipInfo("HMS Euryalus", ShipType.Frigate));
            britishFleet.AddShip(new ShipInfo("HMS Sirius", ShipType.Frigate));
            RegisterFleet(britishFleet);

            // French Navy
            FleetData frenchFleet = new FleetData("Marine Française", "fleet_france", FactionType.France, "brest");
            frenchFleet.AddShip(new ShipInfo("Bucentaure", ShipType.ShipOfTheLine));
            frenchFleet.AddShip(new ShipInfo("Redoutable", ShipType.ShipOfTheLine));
            frenchFleet.AddShip(new ShipInfo("Hermione", ShipType.Frigate));
            frenchFleet.AddShip(new ShipInfo("Sirène", ShipType.Frigate));
            RegisterFleet(frenchFleet);

            // Spanish Navy
            FleetData spanishFleet = new FleetData("Armada Española", "fleet_spain", FactionType.Spain, "cadiz");
            spanishFleet.AddShip(new ShipInfo("Santísima Trinidad", ShipType.ShipOfTheLine));
            spanishFleet.AddShip(new ShipInfo("San Juan Nepomuceno", ShipType.ShipOfTheLine));
            spanishFleet.AddShip(new ShipInfo("Santa Ana", ShipType.Frigate));
            RegisterFleet(spanishFleet);

            // Ottoman Navy
            FleetData ottomanFleet = new FleetData("Osmanlı Donanması", "fleet_ottoman", FactionType.Ottoman, "istanbul_port");
            ottomanFleet.AddShip(new ShipInfo("Mahmudiye", ShipType.ShipOfTheLine));
            ottomanFleet.AddShip(new ShipInfo("Mesudiye", ShipType.Frigate));
            ottomanFleet.AddShip(new ShipInfo("Fethiye", ShipType.Brig));
            RegisterFleet(ottomanFleet);

            Debug.Log($"[Naval] Created {fleets.Count} starting fleets");
        }

        private void RegisterFleet(FleetData fleet)
        {
            fleets[fleet.fleetId] = fleet;
        }

        public void ProcessTurn()
        {
            // Process port construction
            foreach (var port in ports.Values)
            {
                var completedShip = port.ProcessConstruction();
                if (completedShip != null)
                {
                    // Find or create fleet at this port
                    FleetData fleet = GetFleetAtPort(port.portId);
                    if (fleet != null)
                    {
                        fleet.AddShip(completedShip);
                        Debug.Log($"[Naval] {completedShip.shipName} completed at {port.portName}!");
                    }
                }
            }

            // Reset fleet movement
            foreach (var fleet in fleets.Values)
            {
                fleet.ResetMovement();
            }

            // Process blockades
            ProcessBlockades();
        }

        private void ProcessBlockades()
        {
            foreach (var port in ports.Values)
            {
                if (port.isBlockaded && !string.IsNullOrEmpty(port.blockadingFleetId))
                {
                    // Blockade effects: reduce trade, prevent construction
                    var cm = CampaignManager.Instance;
                    if (cm != null && cm.Factions.ContainsKey(port.owner))
                    {
                        cm.Factions[port.owner].gold -= 50f; // Economic damage
                    }
                }
            }
        }

        public FleetData GetFleetAtPort(string portId)
        {
            foreach (var fleet in fleets.Values)
            {
                if (!fleet.isAtSea && fleet.currentPortId == portId)
                    return fleet;
            }
            return null;
        }

        public List<FleetData> GetFactionFleets(FactionType faction)
        {
            var result = new List<FleetData>();
            foreach (var fleet in fleets.Values)
            {
                if (fleet.faction == faction)
                    result.Add(fleet);
            }
            return result;
        }

        public bool CanMoveFleet(FleetData fleet, Vector2 destination)
        {
            if (fleet.movementPoints <= 0) return false;
            
            // Calculate distance
            Vector2 currentPos = fleet.isAtSea ? fleet.seaPosition : ports[fleet.currentPortId].mapPosition;
            float distance = Vector2.Distance(currentPos, destination);
            
            return distance <= 0.2f; // Max move per turn
        }

        public void MoveFleet(FleetData fleet, Vector2 destination)
        {
            if (!CanMoveFleet(fleet, destination)) return;

            fleet.SetToSea(destination);
            fleet.movementPoints--;

            // Check for enemy fleets at destination
            foreach (var otherFleet in fleets.Values)
            {
                if (otherFleet == fleet) continue;
                if (otherFleet.faction == fleet.faction) continue;

                Vector2 otherPos = otherFleet.isAtSea ? otherFleet.seaPosition : 
                    (ports.ContainsKey(otherFleet.currentPortId) ? ports[otherFleet.currentPortId].mapPosition : Vector2.zero);

                if (Vector2.Distance(destination, otherPos) < 0.05f)
                {
                    // Naval battle!
                    TriggerNavalBattle(fleet, otherFleet);
                    break;
                }
            }
        }

        private void TriggerNavalBattle(FleetData attacker, FleetData defender)
        {
            Debug.Log($"[Naval] Naval battle: {attacker.fleetName} vs {defender.fleetName}!");
            
            // Store battle data and load naval battle scene
            if (NapoleonicWars.Core.GameManager.Instance != null)
            {
                // Would need to add fleet IDs to GameManager
                NapoleonicWars.UI.LoadingScreenUI.LoadSceneWithScreen("NavalBattle");
            }
        }
    }
}
