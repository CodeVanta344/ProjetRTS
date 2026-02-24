using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Naval;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Extended naval mechanics: sea zones, strategic blockade effects,
    /// amphibious operations, coastal bombardment, storm damage, and
    /// auto-combat resolution on the campaign map.
    /// Works alongside the existing NavalCampaignManager / FleetData / ShipData.
    /// </summary>

    [System.Serializable]
    public class SeaZone
    {
        public string zoneId;
        public string zoneName;
        public string[] adjacentZones;
        public string[] coastalProvinces;
        public bool isStrait;
        public float stormRisk = 0.05f;

        public SeaZone(string id, string name, string[] adjacent, string[] coastal)
        {
            zoneId = id;
            zoneName = name;
            adjacentZones = adjacent;
            coastalProvinces = coastal;
        }
    }

    public struct NavalCombatResult
    {
        public FactionType winner;
        public int attackerShipsLost;
        public int defenderShipsLost;
        public int attackerCrewLost;
        public int defenderCrewLost;
        public bool attackerRetreated;
        public bool defenderRetreated;
    }

    /// <summary>
    /// Strategic-layer naval extensions. Sea zones, blockade economy effects,
    /// amphibious helpers, coastal bombardment, storm processing.
    /// </summary>
    public static class NavalSystem
    {
        private static Dictionary<string, SeaZone> seaZones = new Dictionary<string, SeaZone>();

        public static Dictionary<string, SeaZone> SeaZones => seaZones;

        static NavalSystem()
        {
            InitializeSeaZones();
        }

        private static void InitializeSeaZones()
        {
            seaZones["english_channel"] = new SeaZone("english_channel", "La Manche",
                new[] { "north_sea", "atlantic_west", "bay_biscay" },
                new[] { "london", "normandy", "picardy", "kent", "devon" });

            seaZones["north_sea"] = new SeaZone("north_sea", "Mer du Nord",
                new[] { "english_channel", "baltic_sea", "norwegian_sea" },
                new[] { "amsterdam", "hamburg", "denmark_east", "scotland" });

            seaZones["baltic_sea"] = new SeaZone("baltic_sea", "Mer Baltique",
                new[] { "north_sea" },
                new[] { "prussia_coast", "sweden_south", "finland", "riga" });

            seaZones["atlantic_west"] = new SeaZone("atlantic_west", "Atlantique Ouest",
                new[] { "english_channel", "bay_biscay" },
                new[] { "brest", "ireland" });

            seaZones["bay_biscay"] = new SeaZone("bay_biscay", "Golfe de Gascogne",
                new[] { "atlantic_west", "atlantic_south" },
                new[] { "bordeaux", "spain_north" });

            seaZones["atlantic_south"] = new SeaZone("atlantic_south", "Atlantique Sud",
                new[] { "bay_biscay", "western_med" },
                new[] { "lisbon", "cadiz", "morocco" });

            seaZones["western_med"] = new SeaZone("western_med", "Méditerranée Occidentale",
                new[] { "atlantic_south", "central_med", "tyrrhenian" },
                new[] { "marseille", "barcelona", "algiers" });

            seaZones["tyrrhenian"] = new SeaZone("tyrrhenian", "Mer Tyrrhénienne",
                new[] { "western_med", "central_med" },
                new[] { "rome", "naples", "sardinia", "corsica" });

            seaZones["central_med"] = new SeaZone("central_med", "Méditerranée Centrale",
                new[] { "western_med", "tyrrhenian", "adriatic", "eastern_med" },
                new[] { "sicily", "tunis", "malta" });

            seaZones["adriatic"] = new SeaZone("adriatic", "Mer Adriatique",
                new[] { "central_med" },
                new[] { "venice", "trieste", "ragusa" });

            seaZones["eastern_med"] = new SeaZone("eastern_med", "Méditerranée Orientale",
                new[] { "central_med", "aegean", "black_sea_entrance" },
                new[] { "egypt", "crete", "cyprus" });

            seaZones["aegean"] = new SeaZone("aegean", "Mer Égée",
                new[] { "eastern_med", "black_sea_entrance" },
                new[] { "athens", "smyrna", "thessaloniki" });

            seaZones["black_sea_entrance"] = new SeaZone("black_sea_entrance", "Détroits",
                new[] { "aegean", "black_sea" },
                new[] { "constantinople" })
                { isStrait = true };

            seaZones["black_sea"] = new SeaZone("black_sea", "Mer Noire",
                new[] { "black_sea_entrance" },
                new[] { "odessa", "sevastopol", "trebizond" });

            seaZones["norwegian_sea"] = new SeaZone("norwegian_sea", "Mer de Norvège",
                new[] { "north_sea" },
                new[] { "norway_south", "norway_north" });
        }

        // === BLOCKADE EFFECTS (strategic layer) ===

        /// <summary>Get the trade-reduction factor caused by blockading a province (0 = none, up to 0.8)</summary>
        public static float GetBlockadeEffect(string provinceId)
        {
            var ncm = NavalCampaignManager.Instance;
            if (ncm == null) return 0f;

            float totalCannons = 0f;
            foreach (var fleet in ncm.Fleets.Values)
            {
                if (fleet.isBlockading && fleet.blockadedPortId != null)
                {
                    // Check if the blockaded port belongs to this province
                    if (ncm.Ports.ContainsKey(fleet.blockadedPortId) &&
                        ncm.Ports[fleet.blockadedPortId].provinceId == provinceId)
                    {
                        totalCannons += fleet.TotalCannons;
                    }
                }
            }

            if (totalCannons <= 0f) return 0f;
            return Mathf.Clamp01(totalCannons / 200f) * 0.8f;
        }

        /// <summary>Is a province currently under naval blockade?</summary>
        public static bool IsProvinceBlockaded(string provinceId)
        {
            return GetBlockadeEffect(provinceId) > 0f;
        }

        // === COASTAL BOMBARDMENT ===

        /// <summary>Bombard a coastal province using ships of the line and frigates in a docked fleet</summary>
        public static void CoastalBombardment(FleetData fleet, string targetProvince, CampaignManager cm)
        {
            if (fleet == null || !cm.Provinces.ContainsKey(targetProvince)) return;

            float bombardmentPower = fleet.TotalCannons * 0.5f;
            if (bombardmentPower <= 0f) return;

            var prov = cm.Provinces[targetProvince];
            prov.ApplyBattleDevastation(bombardmentPower * 0.3f);

            if (prov.garrisonSize > 0)
            {
                int casualties = Mathf.RoundToInt(bombardmentPower * 0.08f);
                prov.garrisonSize = Mathf.Max(0, prov.garrisonSize - casualties);
            }

            Debug.Log($"[Naval] {fleet.faction} bombards {targetProvince} — Power: {bombardmentPower:F0}");
        }

        // === AMPHIBIOUS HELPERS ===

        /// <summary>Get the sea zone(s) that border a given province</summary>
        public static List<string> GetSeaZonesForProvince(string provinceId)
        {
            var result = new List<string>();
            foreach (var zone in seaZones.Values)
            {
                if (zone.coastalProvinces.Contains(provinceId))
                    result.Add(zone.zoneId);
            }
            return result;
        }

        /// <summary>Check if a province is coastal (borders any sea zone)</summary>
        public static bool IsCoastalProvince(string provinceId)
        {
            foreach (var zone in seaZones.Values)
                if (zone.coastalProvinces.Contains(provinceId))
                    return true;
            return false;
        }

        // === NAVAL COMBAT (strategic auto-resolve) ===

        /// <summary>Auto-resolve a naval battle between two fleets on the campaign map</summary>
        public static NavalCombatResult ResolveNavalBattle(FleetData attacker, FleetData defender)
        {
            var result = new NavalCombatResult();
            int rounds = 5;

            float attackerMorale = 100f;
            float defenderMorale = 100f;

            for (int round = 0; round < rounds; round++)
            {
                int aLive = attacker.ships.Count(s => s.hullHP > 0);
                int dLive = defender.ships.Count(s => s.hullHP > 0);
                if (aLive == 0 || dLive == 0) break;

                float attackerFP = attacker.TotalCannons;
                float defenderFP = defender.TotalCannons;

                // Attacker fires → damage defender ships
                foreach (var ship in defender.ships)
                {
                    if (ship.hullHP <= 0) continue;
                    float dmg = (attackerFP / dLive) * Random.Range(0.3f, 1.2f);
                    ship.hullHP -= dmg;
                    int crewLoss = Mathf.RoundToInt(dmg * 0.15f);
                    ship.currentCrew = Mathf.Max(0, ship.currentCrew - crewLoss);
                    result.defenderCrewLost += crewLoss;
                    if (ship.hullHP <= 0) result.defenderShipsLost++;
                }

                // Defender fires → damage attacker ships
                foreach (var ship in attacker.ships)
                {
                    if (ship.hullHP <= 0) continue;
                    float dmg = (defenderFP / aLive) * Random.Range(0.3f, 1.2f);
                    ship.hullHP -= dmg;
                    int crewLoss = Mathf.RoundToInt(dmg * 0.15f);
                    ship.currentCrew = Mathf.Max(0, ship.currentCrew - crewLoss);
                    result.attackerCrewLost += crewLoss;
                    if (ship.hullHP <= 0) result.attackerShipsLost++;
                }

                attackerMorale -= result.attackerShipsLost * 15f;
                defenderMorale -= result.defenderShipsLost * 15f;

                if (attackerMorale <= 0) { result.attackerRetreated = true; result.winner = defender.faction; break; }
                if (defenderMorale <= 0) { result.defenderRetreated = true; result.winner = attacker.faction; break; }
            }

            if (!result.attackerRetreated && !result.defenderRetreated)
                result.winner = attackerMorale > defenderMorale ? attacker.faction : defender.faction;

            // Remove sunk ships
            attacker.ships.RemoveAll(s => s.hullHP <= 0);
            defender.ships.RemoveAll(s => s.hullHP <= 0);

            Debug.Log($"[Naval] Battle: {attacker.faction} vs {defender.faction} — Winner: {result.winner} " +
                      $"(Ships lost: {result.attackerShipsLost}/{result.defenderShipsLost})");
            return result;
        }

        // === STORM PROCESSING ===

        /// <summary>Process storms for all fleets at sea (called by NavalCampaignManager or EndPlayerTurn)</summary>
        public static void ProcessStorms(CampaignManager cm)
        {
            var ncm = NavalCampaignManager.Instance;
            if (ncm == null) return;

            foreach (var fleet in ncm.Fleets.Values)
            {
                if (!fleet.isAtSea) continue;

                // Find the closest sea zone by checking coastal provinces
                float stormChance = 0.05f;
                if (SeasonSystem.CurrentSeason == Season.Winter) stormChance *= 3f;
                else if (SeasonSystem.CurrentSeason == Season.Autumn) stormChance *= 2f;

                if (Random.value < stormChance)
                {
                    foreach (var ship in fleet.ships)
                    {
                        float dmg = Random.Range(10f, 50f);
                        ship.hullHP -= dmg;
                    }
                    fleet.ships.RemoveAll(s => s.hullHP <= 0);
                    cm.AddFactionEvent(fleet.faction, $"Tempête! {fleet.fleetName} subit des dégâts");
                }
            }
        }

        // === TURN PROCESSING (called from CampaignManager) ===

        public static void ProcessNavalTurn(CampaignManager cm)
        {
            // Delegate core fleet processing to the existing NavalCampaignManager
            NavalCampaignManager.Instance?.ProcessTurn();

            // Storm processing for fleets at sea
            ProcessStorms(cm);
        }

        // === DISPLAY HELPERS ===

        public static string GetShipTypeName(ShipType type) => type switch
        {
            ShipType.Frigate => "Frégate",
            ShipType.ShipOfTheLine => "Vaisseau de Ligne",
            ShipType.Brig => "Brick",
            ShipType.Sloop => "Sloop",
            ShipType.Fireship => "Brûlot",
            ShipType.Transport => "Transport",
            _ => "?"
        };

        public static Color GetFleetColor(FactionType faction) => faction switch
        {
            FactionType.France => new Color(0.2f, 0.2f, 0.8f),
            FactionType.Britain => Color.red,
            FactionType.Russia => Color.green,
            FactionType.Spain => Color.yellow,
            FactionType.Ottoman => new Color(0.8f, 0.2f, 0.2f),
            _ => Color.white
        };

        /// <summary>Get total fleet strength for a faction</summary>
        public static float GetNavalStrength(FactionType faction)
        {
            var ncm = NavalCampaignManager.Instance;
            if (ncm == null) return 0f;
            float total = 0f;
            foreach (var fleet in ncm.GetFactionFleets(faction))
                total += fleet.TotalCannons;
            return total;
        }
    }
}
