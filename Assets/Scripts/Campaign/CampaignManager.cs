using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NapoleonicWars.Data;
using NapoleonicWars.Units;
using static NapoleonicWars.Campaign.ProductionChainManager;

namespace NapoleonicWars.Campaign
{
    public class CampaignManager : MonoBehaviour
    {
        public static CampaignManager Instance { get; private set; }

        [Header("Campaign State")]
        private int currentTurn = 1;
        private FactionType currentFactionTurn;
        private CampaignPhase currentPhase = CampaignPhase.PlayerTurn;

        // Real-time clock
        private CampaignClock campaignClock;

        [Header("Map Alignment")]
        [Tooltip("Adjust these to align the coded province coordinates with your custom heightmap image")]
        public Vector2 mapOffset = Vector2.zero;
        public Vector2 mapScale = Vector2.one;

        private Dictionary<string, ProvinceData> provinces = new Dictionary<string, ProvinceData>();
        private Dictionary<string, ArmyData> armies = new Dictionary<string, ArmyData>();
        private Dictionary<string, GeneralData> generals = new Dictionary<string, GeneralData>();
        private Dictionary<FactionType, FactionData> factions = new Dictionary<FactionType, FactionData>();
        private Dictionary<string, CityData> cities = new Dictionary<string, CityData>();

        private FactionType playerFaction = FactionType.France;
        private HistoricalEventSystem eventSystem;

        // Public accessors
        public int CurrentTurn => currentTurn;
        public FactionType CurrentFactionTurn => currentFactionTurn;
        public CampaignPhase CurrentPhase => currentPhase;
        public FactionType PlayerFaction => playerFaction;
        public Dictionary<string, ProvinceData> Provinces => provinces;
        public Dictionary<string, ArmyData> Armies => armies;
        public Dictionary<string, GeneralData> Generals => generals;
        public Dictionary<FactionType, FactionData> Factions => factions;
        public Dictionary<string, CityData> Cities => cities;
        public HistoricalEventSystem EventSystem => eventSystem;

        // Events
        public delegate void TurnChanged(int turn, FactionType faction);
        public event TurnChanged OnTurnChanged;

        public delegate void BattleTriggered(ArmyData attacker, ArmyData defender, ProvinceData province);
        public event BattleTriggered OnBattleTriggered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize immediately so CampaignMap3D can access provinces in its Start()
            InitializeCampaign();
        }

        private void Start()
        {
            // Moved to Awake to fix initialization order issues
        }

        private void InitializeCampaign()
        {
            // Load player faction from selection screen (defaults to France if not set)
            int savedFaction = PlayerPrefs.GetInt("SelectedFaction", (int)FactionType.France);
            playerFaction = (FactionType)savedFaction;
            
            try { CreateFactions(); Debug.Log($"[CampaignManager] Factions created: {factions.Count}"); }
            catch (System.Exception e) { Debug.LogError($"[CampaignManager] CreateFactions failed: {e.Message}\n{e.StackTrace}"); }
            
            try { CreateMap(); Debug.Log($"[CampaignManager] Map created: {provinces.Count} provinces"); }
            catch (System.Exception e) { Debug.LogError($"[CampaignManager] CreateMap failed: {e.Message}\n{e.StackTrace}"); }
            
            try { CreateCities(); }
            catch (System.Exception e) { Debug.LogError($"[CampaignManager] CreateCities failed: {e.Message}\n{e.StackTrace}"); }
            
            try { CreateStartingArmies(); Debug.Log($"[CampaignManager] Armies created: {armies.Count}"); }
            catch (System.Exception e) { Debug.LogError($"[CampaignManager] CreateStartingArmies failed: {e.Message}\n{e.StackTrace}"); }

            try { CreateStartingGenerals(); Debug.Log($"[CampaignManager] Generals created: {generals.Count}"); }
            catch (System.Exception e) { Debug.LogError($"[CampaignManager] CreateStartingGenerals failed: {e.Message}\n{e.StackTrace}"); }

            try { eventSystem = new HistoricalEventSystem(); }
            catch (System.Exception e) { Debug.LogError($"[CampaignManager] HistoricalEventSystem init failed: {e.Message}"); }

            // Initialize season system
            SeasonSystem.Initialize(1700);

            currentFactionTurn = playerFaction;
            currentPhase = CampaignPhase.PlayerTurn;
            currentTurn = 1;

            // === INITIALIZE HOI4 SYSTEMS ===
            try { InitializeHoI4Systems(); }
            catch (System.Exception e) { Debug.LogError($"[CampaignManager] HoI4 systems init failed: {e.Message}\n{e.StackTrace}"); }

            // === REAL-TIME CLOCK ===
            campaignClock = gameObject.AddComponent<CampaignClock>();
            campaignClock.SecondsPerDay = 5f;     // 5 real seconds = 1 game day
            campaignClock.DaysPerSeason = 90;      // 90 days per season
            campaignClock.OnDayTick += ProcessDayTick;
            campaignClock.OnSeasonTick += ProcessSeasonTick;
            campaignClock.Play(); // Start running — 1 day per 5 seconds

            Debug.Log($"[CampaignManager] Campaign started (REAL-TIME). Day-based: 5s/day. {playerFaction}. Provinces: {provinces.Count}, Cities: {cities.Count}, Armies: {armies.Count}");
        }

        private void CreateFactions()
        {
            // Load player faction from selection screen
            int savedFaction = PlayerPrefs.GetInt("SelectedFaction", (int)FactionType.France);
            FactionType playerSelectedFaction = (FactionType)savedFaction;
            
            factions[FactionType.France] = new FactionData(FactionType.France, "Kingdom of France", playerSelectedFaction == FactionType.France) { gold = 2000f, food = 800f, iron = 300f };
            factions[FactionType.Britain] = new FactionData(FactionType.Britain, "Kingdom of Great Britain", playerSelectedFaction == FactionType.Britain) { gold = 2500f, food = 600f, iron = 250f };
            factions[FactionType.Prussia] = new FactionData(FactionType.Prussia, "Brandenburg-Prussia", playerSelectedFaction == FactionType.Prussia) { gold = 1200f, food = 600f, iron = 300f };
            factions[FactionType.Russia] = new FactionData(FactionType.Russia, "Tsardom of Russia", playerSelectedFaction == FactionType.Russia) { gold = 1800f, food = 900f, iron = 200f };
            factions[FactionType.Austria] = new FactionData(FactionType.Austria, "Habsburg Monarchy", playerSelectedFaction == FactionType.Austria) { gold = 1700f, food = 750f, iron = 280f };
            factions[FactionType.Spain] = new FactionData(FactionType.Spain, "Spanish Monarchy", playerSelectedFaction == FactionType.Spain) { gold = 2200f, food = 650f, iron = 180f };
            factions[FactionType.Ottoman] = new FactionData(FactionType.Ottoman, "Ottoman Empire", playerSelectedFaction == FactionType.Ottoman) { gold = 1900f, food = 850f, iron = 220f };
            factions[FactionType.Portugal] = new FactionData(FactionType.Portugal, "Kingdom of Portugal", playerSelectedFaction == FactionType.Portugal) { gold = 1600f, food = 500f, iron = 150f };
            factions[FactionType.Sweden] = new FactionData(FactionType.Sweden, "Swedish Empire", playerSelectedFaction == FactionType.Sweden) { gold = 1400f, food = 550f, iron = 350f };
            factions[FactionType.Denmark] = new FactionData(FactionType.Denmark, "Denmark-Norway", playerSelectedFaction == FactionType.Denmark) { gold = 1300f, food = 600f, iron = 200f };
            factions[FactionType.Poland] = new FactionData(FactionType.Poland, "Polish-Lithuanian Commonwealth", playerSelectedFaction == FactionType.Poland) { gold = 1500f, food = 850f, iron = 180f };
            factions[FactionType.Venice] = new FactionData(FactionType.Venice, "Republic of Venice", playerSelectedFaction == FactionType.Venice) { gold = 1800f, food = 450f, iron = 150f };
            factions[FactionType.Dutch] = new FactionData(FactionType.Dutch, "Dutch Republic", playerSelectedFaction == FactionType.Dutch) { gold = 2300f, food = 400f, iron = 120f };
            factions[FactionType.Bavaria] = new FactionData(FactionType.Bavaria, "Electorate of Bavaria", playerSelectedFaction == FactionType.Bavaria) { gold = 1000f, food = 500f, iron = 150f };
            factions[FactionType.Saxony] = new FactionData(FactionType.Saxony, "Electorate of Saxony", playerSelectedFaction == FactionType.Saxony) { gold = 1100f, food = 450f, iron = 200f };
            factions[FactionType.PapalStates] = new FactionData(FactionType.PapalStates, "Papal States", playerSelectedFaction == FactionType.PapalStates) { gold = 1200f, food = 400f, iron = 100f };
            factions[FactionType.Savoy] = new FactionData(FactionType.Savoy, "Duchy of Savoy", playerSelectedFaction == FactionType.Savoy) { gold = 1000f, food = 450f, iron = 150f };
            factions[FactionType.Switzerland] = new FactionData(FactionType.Switzerland, "Swiss Confederation", playerSelectedFaction == FactionType.Switzerland) { gold = 1500f, food = 400f, iron = 150f };
            factions[FactionType.Genoa] = new FactionData(FactionType.Genoa, "Republic of Genoa", playerSelectedFaction == FactionType.Genoa) { gold = 1600f, food = 300f, iron = 100f };
            factions[FactionType.Tuscany] = new FactionData(FactionType.Tuscany, "Grand Duchy of Tuscany", playerSelectedFaction == FactionType.Tuscany) { gold = 1400f, food = 450f, iron = 120f };
            factions[FactionType.Hanover] = new FactionData(FactionType.Hanover, "Electorate of Hanover", playerSelectedFaction == FactionType.Hanover) { gold = 1200f, food = 500f, iron = 150f };
            factions[FactionType.Modena] = new FactionData(FactionType.Modena, "Duchy of Modena", playerSelectedFaction == FactionType.Modena) { gold = 800f, food = 350f, iron = 80f };
            factions[FactionType.Parma] = new FactionData(FactionType.Parma, "Duchy of Parma", playerSelectedFaction == FactionType.Parma) { gold = 800f, food = 350f, iron = 80f };
            factions[FactionType.Lorraine] = new FactionData(FactionType.Lorraine, "Duchy of Lorraine", playerSelectedFaction == FactionType.Lorraine) { gold = 900f, food = 400f, iron = 120f };

            // Set initial diplomacy (1700 context: Great Northern War starting, War of Spanish Succession looming)
            SetDiplomacy(FactionType.France, FactionType.Britain, DiplomacyState.Hostile);
            SetDiplomacy(FactionType.France, FactionType.Austria, DiplomacyState.Hostile);
            SetDiplomacy(FactionType.France, FactionType.Dutch, DiplomacyState.Hostile);
            SetDiplomacy(FactionType.France, FactionType.Spain, DiplomacyState.Alliance);
            SetDiplomacy(FactionType.France, FactionType.Bavaria, DiplomacyState.Alliance);

            SetDiplomacy(FactionType.Britain, FactionType.Dutch, DiplomacyState.Alliance);
            SetDiplomacy(FactionType.Britain, FactionType.Austria, DiplomacyState.Alliance);
            SetDiplomacy(FactionType.Britain, FactionType.Spain, DiplomacyState.Hostile);

            SetDiplomacy(FactionType.Sweden, FactionType.Russia, DiplomacyState.War);
            SetDiplomacy(FactionType.Sweden, FactionType.Denmark, DiplomacyState.War);
            SetDiplomacy(FactionType.Sweden, FactionType.Poland, DiplomacyState.War);
            SetDiplomacy(FactionType.Sweden, FactionType.Saxony, DiplomacyState.War);

            SetDiplomacy(FactionType.Russia, FactionType.Denmark, DiplomacyState.Alliance);
            SetDiplomacy(FactionType.Russia, FactionType.Poland, DiplomacyState.Alliance);
            SetDiplomacy(FactionType.Russia, FactionType.Saxony, DiplomacyState.Alliance);
            SetDiplomacy(FactionType.Russia, FactionType.Ottoman, DiplomacyState.Hostile);

            SetDiplomacy(FactionType.Austria, FactionType.Ottoman, DiplomacyState.Hostile);
            SetDiplomacy(FactionType.Venice, FactionType.Ottoman, DiplomacyState.Hostile);

            SetDiplomacy(FactionType.Spain, FactionType.Portugal, DiplomacyState.Neutral);
            SetDiplomacy(FactionType.Britain, FactionType.Portugal, DiplomacyState.Alliance);
        }

        private void SetDiplomacy(FactionType a, FactionType b, DiplomacyState state)
        {
            if (!factions[a].relations.ContainsKey(b))
                factions[a].relations[b] = new DiplomaticRelation(state);
            else
                factions[a].relations[b].state = state;

            if (!factions[b].relations.ContainsKey(a))
                factions[b].relations[a] = new DiplomaticRelation(state);
            else
                factions[b].relations[a].state = state;
        }

        private void CreateMap()
        {
            // =====================================================================
            // GIANT EUROPE CAMPAIGN MAP - Geographic Accuracy
            // Realistic positions based on European geography
            // Map dimensions: 200 x 150 (gigantic scale)
            // Coordinates: 0-1 normalized, will be scaled by mapWidth/mapHeight
            // Geographic reference: Atlantic West (0.0) to Urals East (1.0)
            //                       Scandinavia North (1.0) to Mediterranean South (0.0)
            // =====================================================================
            


            // ===================== FRANCE =====================
            // France core in EuropeLandMask: E(0.35, 0.58) — but cities spread from 0.24 to 0.35
            AddProvince("paris", "Paris", FactionType.France, new Vector2(0.30f, 0.57f),
                new[] { "normandy", "picardy", "champagne", "orleans", "burgundy" });
            AddProvince("normandy", "Rouen", FactionType.France, new Vector2(0.27f, 0.58f),
                new[] { "paris", "brittany", "picardy", "orleans" });
            AddProvince("brittany", "Rennes", FactionType.France, new Vector2(0.22f, 0.56f),
                new[] { "normandy", "orleans", "aquitaine" });
            AddProvince("picardy", "Amiens", FactionType.France, new Vector2(0.30f, 0.58f),
                new[] { "paris", "normandy", "flanders", "champagne" });
            AddProvince("champagne", "Reims", FactionType.France, new Vector2(0.33f, 0.56f),
                new[] { "paris", "picardy", "burgundy", "lorraine", "flanders" });
            AddProvince("orleans", "Orléans", FactionType.France, new Vector2(0.29f, 0.54f),
                new[] { "paris", "normandy", "brittany", "aquitaine", "auvergne", "burgundy" });
            AddProvince("burgundy", "Dijon", FactionType.France, new Vector2(0.33f, 0.53f),
                new[] { "paris", "champagne", "orleans", "auvergne", "lyon", "franche_comte" });
            AddProvince("aquitaine", "Bordeaux", FactionType.France, new Vector2(0.25f, 0.49f),
                new[] { "brittany", "orleans", "auvergne", "toulouse", "navarre" });
            AddProvince("auvergne", "Clermont", FactionType.France, new Vector2(0.30f, 0.51f),
                new[] { "orleans", "burgundy", "aquitaine", "lyon", "toulouse", "languedoc" });
            AddProvince("lyon", "Lyon", FactionType.France, new Vector2(0.33f, 0.51f),
                new[] { "burgundy", "auvergne", "franche_comte", "savoy", "dauphine", "languedoc" });
            AddProvince("toulouse", "Toulouse", FactionType.France, new Vector2(0.27f, 0.46f),
                new[] { "aquitaine", "auvergne", "languedoc", "aragon", "catalonia" });
            AddProvince("languedoc", "Montpellier", FactionType.France, new Vector2(0.30f, 0.45f),
                new[] { "auvergne", "lyon", "toulouse", "dauphine", "provence", "catalonia" });
            AddProvince("dauphine", "Grenoble", FactionType.France, new Vector2(0.34f, 0.48f),
                new[] { "lyon", "languedoc", "savoy", "provence", "piedmont" });
            AddProvince("provence", "Marseille", FactionType.France, new Vector2(0.32f, 0.44f),
                new[] { "languedoc", "dauphine", "nice", "piedmont" });
            AddProvince("franche_comte", "Besançon", FactionType.France, new Vector2(0.34f, 0.53f),
                new[] { "burgundy", "lyon", "lorraine", "alsace", "switzerland", "savoy" });
            AddProvince("lorraine", "Nancy", FactionType.France, new Vector2(0.35f, 0.56f),
                new[] { "champagne", "franche_comte", "alsace", "palatinate", "flanders" });
            AddProvince("alsace", "Strasbourg", FactionType.France, new Vector2(0.36f, 0.55f),
                new[] { "lorraine", "franche_comte", "baden", "wurttemberg" });
            AddProvince("corsica", "Ajaccio", FactionType.Genoa, new Vector2(0.38f, 0.40f),
                new[] { "sardinia", "genoa" });
            
            // ===================== LOW COUNTRIES =====================
            AddProvince("flanders", "Brussels", FactionType.Austria, new Vector2(0.34f, 0.66f),
                new[] { "picardy", "champagne", "lorraine", "holland", "westphalia" });
            AddProvince("holland", "Amsterdam", FactionType.Dutch, new Vector2(0.34f, 0.66f),
                new[] { "flanders", "westphalia", "hannover" });
            
            // ===================== BRITISH ISLES =====================
            // British Isles in EuropeLandMask: SE England (0.19,0.66), Midlands (0.16,0.68), etc.
            AddProvince("london", "London", FactionType.Britain, new Vector2(0.20f, 0.63f),
                new[] { "midlands", "kent", "east_anglia" });
            AddProvince("kent", "Canterbury", FactionType.Britain, new Vector2(0.22f, 0.62f),
                new[] { "london", "east_anglia" });
            AddProvince("east_anglia", "Norwich", FactionType.Britain, new Vector2(0.22f, 0.66f),
                new[] { "london", "kent", "midlands", "yorkshire" });
            AddProvince("cornwall", "Plymouth", FactionType.Britain, new Vector2(0.17f, 0.62f),
                new[] { "wales", "london", "midlands" });
            AddProvince("midlands", "Birmingham", FactionType.Britain, new Vector2(0.17f, 0.67f),
                new[] { "london", "kent", "east_anglia", "cornwall", "wales", "yorkshire" });
            AddProvince("wales", "Cardiff", FactionType.Britain, new Vector2(0.13f, 0.67f),
                new[] { "cornwall", "midlands", "yorkshire", "ireland" });
            AddProvince("yorkshire", "York", FactionType.Britain, new Vector2(0.17f, 0.72f),
                new[] { "midlands", "east_anglia", "wales", "cumbria", "scotland" });
            AddProvince("cumbria", "Carlisle", FactionType.Britain, new Vector2(0.16f, 0.74f),
                new[] { "yorkshire", "scotland", "ireland" });
            AddProvince("scotland", "Edinburgh", FactionType.Britain, new Vector2(0.17f, 0.75f),
                new[] { "yorkshire", "cumbria", "highlands" });
            AddProvince("highlands", "Inverness", FactionType.Britain, new Vector2(0.17f, 0.75f),
                new[] { "scotland" });
            AddProvince("ireland", "Dublin", FactionType.Britain, new Vector2(0.12f, 0.69f),
                new[] { "wales", "cumbria", "west_ireland" });
            AddProvince("west_ireland", "Galway", FactionType.Britain, new Vector2(0.12f, 0.69f),
                new[] { "ireland" });
            
            // ===================== SPAIN & PORTUGAL =====================
            // Iberia core in EuropeLandMask: E(0.18, 0.39)
            AddProvince("castile", "Madrid", FactionType.Spain, new Vector2(0.18f, 0.38f),
                new[] { "leon", "aragon", "andalusia", "estremadura", "valencia" });
            AddProvince("leon", "León", FactionType.Spain, new Vector2(0.15f, 0.40f),
                new[] { "castile", "galicia_spain", "asturias", "estremadura", "portugal_north" });
            AddProvince("galicia_spain", "Santiago", FactionType.Spain, new Vector2(0.11f, 0.43f),
                new[] { "leon", "asturias", "portugal_north" });
            AddProvince("asturias", "Oviedo", FactionType.Spain, new Vector2(0.15f, 0.44f),
                new[] { "galicia_spain", "leon", "navarre" });
            AddProvince("navarre", "Pamplona", FactionType.Spain, new Vector2(0.22f, 0.44f),
                new[] { "asturias", "aragon", "aquitaine" });
            AddProvince("aragon", "Zaragoza", FactionType.Spain, new Vector2(0.22f, 0.40f),
                new[] { "navarre", "castile", "catalonia", "valencia", "toulouse" });
            AddProvince("catalonia", "Barcelona", FactionType.Spain, new Vector2(0.26f, 0.41f),
                new[] { "aragon", "valencia", "toulouse", "languedoc" });
            AddProvince("valencia", "Valencia", FactionType.Spain, new Vector2(0.23f, 0.37f),
                new[] { "catalonia", "aragon", "castile", "murcia" });
            AddProvince("murcia", "Murcia", FactionType.Spain, new Vector2(0.21f, 0.34f),
                new[] { "valencia", "andalusia", "castile" });
            AddProvince("andalusia", "Seville", FactionType.Spain, new Vector2(0.16f, 0.33f),
                new[] { "murcia", "castile", "estremadura", "algarve", "gibraltar" });
            AddProvince("estremadura", "Badajoz", FactionType.Spain, new Vector2(0.14f, 0.38f),
                new[] { "andalusia", "castile", "leon", "alentejo", "portugal_central" });
            AddProvince("gibraltar", "Gibraltar", FactionType.Britain, new Vector2(0.17f, 0.30f),
                new[] { "andalusia" });
            AddProvince("portugal_north", "Porto", FactionType.Portugal, new Vector2(0.11f, 0.42f),
                new[] { "galicia_spain", "leon", "portugal_central" });
            AddProvince("portugal_central", "Lisbon", FactionType.Portugal, new Vector2(0.10f, 0.38f),
                new[] { "portugal_north", "estremadura", "alentejo" });
            AddProvince("alentejo", "Évora", FactionType.Portugal, new Vector2(0.12f, 0.36f),
                new[] { "portugal_central", "estremadura", "algarve" });
            AddProvince("algarve", "Faro", FactionType.Portugal, new Vector2(0.12f, 0.33f),
                new[] { "alentejo", "andalusia" });
            
            // ===================== ITALIAN STATES =====================
            // Italy in EuropeLandMask: NW Italy ~(0.36,0.48), boot axis ~(0.40-0.47, 0.35-0.45)
            AddProvince("piedmont", "Turin", FactionType.Savoy, new Vector2(0.36f, 0.49f),
                new[] { "savoy", "dauphine", "provence", "nice", "genoa", "milan" });
            AddProvince("savoy", "Chambéry", FactionType.Savoy, new Vector2(0.34f, 0.51f),
                new[] { "piedmont", "lyon", "dauphine", "switzerland", "franche_comte" });
            AddProvince("nice", "Nice", FactionType.Savoy, new Vector2(0.35f, 0.46f),
                new[] { "provence", "piedmont", "genoa" });
            AddProvince("genoa", "Genoa", FactionType.Genoa, new Vector2(0.37f, 0.46f),
                new[] { "nice", "piedmont", "milan", "parma", "tuscany", "corsica" });
            AddProvince("milan", "Milan", FactionType.Austria, new Vector2(0.38f, 0.49f),
                new[] { "piedmont", "genoa", "parma", "venice", "tyrol", "switzerland" });
            AddProvince("venice", "Venice", FactionType.Venice, new Vector2(0.42f, 0.48f),
                new[] { "milan", "parma", "romagna", "tyrol", "carinthia", "illyria" });
            AddProvince("parma", "Parma", FactionType.Parma, new Vector2(0.39f, 0.46f),
                new[] { "milan", "genoa", "venice", "romagna", "tuscany" });
            AddProvince("tuscany", "Florence", FactionType.Tuscany, new Vector2(0.40f, 0.44f),
                new[] { "genoa", "parma", "romagna", "papal_states" });
            AddProvince("romagna", "Bologna", FactionType.PapalStates, new Vector2(0.41f, 0.45f),
                new[] { "venice", "parma", "tuscany", "papal_states" });
            AddProvince("papal_states", "Rome", FactionType.PapalStates, new Vector2(0.42f, 0.40f),
                new[] { "tuscany", "romagna", "naples", "abruzzo" });
            AddProvince("abruzzo", "L'Aquila", FactionType.Spain, new Vector2(0.44f, 0.39f),
                new[] { "papal_states", "naples", "apulia" });
            AddProvince("naples", "Naples", FactionType.Spain, new Vector2(0.46f, 0.37f),
                new[] { "papal_states", "abruzzo", "apulia", "calabria", "sicily" });
            AddProvince("apulia", "Bari", FactionType.Spain, new Vector2(0.48f, 0.37f),
                new[] { "abruzzo", "naples", "calabria" });
            AddProvince("calabria", "Reggio", FactionType.Spain, new Vector2(0.47f, 0.29f),
                new[] { "naples", "apulia", "sicily" });
            AddProvince("sicily", "Palermo", FactionType.Spain, new Vector2(0.45f, 0.28f),
                new[] { "naples", "calabria", "tunis", "malta" });
            AddProvince("sardinia", "Cagliari", FactionType.Spain, new Vector2(0.37f, 0.36f),
                new[] { "corsica" });
            AddProvince("malta", "Valletta", FactionType.Spain, new Vector2(0.45f, 0.28f),
                new[] { "sicily" });
            
            // ===================== SWITZERLAND & TYROL =====================
            // Alps in EuropeLandMask: E(0.38, 0.52)
            AddProvince("switzerland", "Bern", FactionType.Switzerland, new Vector2(0.36f, 0.53f),
                new[] { "franche_comte", "savoy", "milan", "tyrol", "baden", "wurttemberg", "bavaria" });
            AddProvince("tyrol", "Innsbruck", FactionType.Austria, new Vector2(0.40f, 0.53f),
                new[] { "switzerland", "milan", "venice", "carinthia", "salzburg", "bavaria" });
            
            // ===================== GERMAN STATES (PRUSSIA & MINORS) =====================
            // Central Europe in EuropeLandMask: E(0.42, 0.66)
            AddProvince("baden", "Karlsruhe", FactionType.Austria, new Vector2(0.37f, 0.57f),
                new[] { "alsace", "switzerland", "wurttemberg", "palatinate" });
            AddProvince("wurttemberg", "Stuttgart", FactionType.Austria, new Vector2(0.38f, 0.57f),
                new[] { "baden", "switzerland", "bavaria", "franconia", "palatinate" });
            AddProvince("bavaria", "Munich", FactionType.Bavaria, new Vector2(0.40f, 0.55f),
                new[] { "wurttemberg", "switzerland", "tyrol", "salzburg", "bohemia", "franconia" });
            AddProvince("palatinate", "Mannheim", FactionType.Bavaria, new Vector2(0.36f, 0.60f),
                new[] { "alsace", "lorraine", "baden", "wurttemberg", "franconia", "hessian_states" });
            AddProvince("franconia", "Nuremberg", FactionType.Bavaria, new Vector2(0.40f, 0.59f),
                new[] { "palatinate", "wurttemberg", "bavaria", "bohemia", "saxony", "thuringia" });
            AddProvince("westphalia", "Münster", FactionType.Hanover, new Vector2(0.36f, 0.65f),
                new[] { "flanders", "holland", "hannover", "hessian_states", "palatinate" });
            AddProvince("hessian_states", "Kassel", FactionType.Hanover, new Vector2(0.38f, 0.63f),
                new[] { "westphalia", "hannover", "thuringia", "franconia", "palatinate" });
            AddProvince("thuringia", "Erfurt", FactionType.Saxony, new Vector2(0.40f, 0.62f),
                new[] { "hessian_states", "hannover", "saxony", "franconia" });
            AddProvince("hannover", "Hanover", FactionType.Hanover, new Vector2(0.38f, 0.67f),
                new[] { "holland", "westphalia", "hessian_states", "thuringia", "saxony", "magdeburg", "mecklenburg", "holstein" });
            AddProvince("saxony", "Dresden", FactionType.Saxony, new Vector2(0.43f, 0.62f),
                new[] { "franconia", "thuringia", "hannover", "magdeburg", "brandenburg", "silesia", "bohemia" });
            AddProvince("magdeburg", "Magdeburg", FactionType.Prussia, new Vector2(0.42f, 0.65f),
                new[] { "hannover", "saxony", "brandenburg", "mecklenburg" });
            AddProvince("brandenburg", "Berlin", FactionType.Prussia, new Vector2(0.44f, 0.66f),
                new[] { "magdeburg", "saxony", "silesia", "posen", "pomerania", "mecklenburg" });
            AddProvince("mecklenburg", "Schwerin", FactionType.Hanover, new Vector2(0.42f, 0.69f),
                new[] { "holstein", "hannover", "magdeburg", "brandenburg", "pomerania" });
            AddProvince("pomerania", "Stettin", FactionType.Prussia, new Vector2(0.46f, 0.69f),
                new[] { "mecklenburg", "brandenburg", "west_prussia" });
            AddProvince("silesia", "Breslau", FactionType.Prussia, new Vector2(0.47f, 0.61f),
                new[] { "saxony", "brandenburg", "posen", "poland", "moravia", "bohemia" });
            AddProvince("posen", "Posen", FactionType.Poland, new Vector2(0.49f, 0.64f),
                new[] { "brandenburg", "pomerania", "west_prussia", "poland", "silesia" });
            AddProvince("west_prussia", "Danzig", FactionType.Poland, new Vector2(0.50f, 0.68f),
                new[] { "pomerania", "posen", "poland", "east_prussia" });
            AddProvince("east_prussia", "Königsberg", FactionType.Prussia, new Vector2(0.54f, 0.70f),
                new[] { "west_prussia", "poland", "lithuania", "courland" });
            
            // ===================== AUSTRIAN EMPIRE =====================
            AddProvince("salzburg", "Salzburg", FactionType.Austria, new Vector2(0.42f, 0.54f),
                new[] { "bavaria", "tyrol", "carinthia", "styria", "upper_austria" });
            AddProvince("upper_austria", "Linz", FactionType.Austria, new Vector2(0.44f, 0.55f),
                new[] { "bavaria", "bohemia", "lower_austria", "styria", "salzburg" });
            AddProvince("lower_austria", "Vienna", FactionType.Austria, new Vector2(0.46f, 0.55f),
                new[] { "upper_austria", "bohemia", "moravia", "hungary", "styria" });
            AddProvince("styria", "Graz", FactionType.Austria, new Vector2(0.44f, 0.52f),
                new[] { "upper_austria", "lower_austria", "hungary", "croatia", "carinthia", "salzburg" });
            AddProvince("carinthia", "Klagenfurt", FactionType.Austria, new Vector2(0.43f, 0.51f),
                new[] { "tyrol", "salzburg", "styria", "illyria", "venice" });
            AddProvince("illyria", "Trieste", FactionType.Austria, new Vector2(0.44f, 0.49f),
                new[] { "venice", "carinthia", "croatia", "dalmatia" });
            AddProvince("bohemia", "Prague", FactionType.Austria, new Vector2(0.44f, 0.59f),
                new[] { "bavaria", "franconia", "saxony", "silesia", "moravia", "lower_austria", "upper_austria" });
            AddProvince("moravia", "Brno", FactionType.Austria, new Vector2(0.47f, 0.57f),
                new[] { "bohemia", "silesia", "galicia", "hungary", "lower_austria" });
            AddProvince("galicia", "Lemberg", FactionType.Poland, new Vector2(0.54f, 0.58f),
                new[] { "moravia", "poland", "volhynia", "podolia", "transylvania", "hungary" });
            AddProvince("hungary", "Buda", FactionType.Austria, new Vector2(0.50f, 0.53f),
                new[] { "lower_austria", "moravia", "galicia", "transylvania", "banat", "slavonia", "croatia", "styria" });
            AddProvince("croatia", "Agram", FactionType.Austria, new Vector2(0.46f, 0.50f),
                new[] { "styria", "hungary", "slavonia", "bosnia", "dalmatia", "illyria" });
            AddProvince("dalmatia", "Zara", FactionType.Venice, new Vector2(0.46f, 0.46f),
                new[] { "illyria", "croatia", "bosnia" });
            AddProvince("slavonia", "Essek", FactionType.Austria, new Vector2(0.50f, 0.50f),
                new[] { "croatia", "hungary", "banat", "serbia", "bosnia" });
            AddProvince("transylvania", "Klausenburg", FactionType.Austria, new Vector2(0.55f, 0.54f),
                new[] { "hungary", "galicia", "podolia", "moldavia", "wallachia", "banat" });
            AddProvince("banat", "Temesvar", FactionType.Austria, new Vector2(0.52f, 0.49f),
                new[] { "hungary", "transylvania", "wallachia", "serbia", "slavonia" });
            
            // ===================== SCANDINAVIA =====================
            // Scandinavia in EuropeLandMask: ER(0.42, 0.82) main axis
            AddProvince("holstein", "Kiel", FactionType.Denmark, new Vector2(0.40f, 0.72f),
                new[] { "hannover", "mecklenburg", "denmark" });
            AddProvince("denmark", "Copenhagen", FactionType.Denmark, new Vector2(0.42f, 0.74f),
                new[] { "holstein", "norway", "sweden_south" });
            AddProvince("norway", "Christiania", FactionType.Denmark, new Vector2(0.38f, 0.82f),
                new[] { "denmark", "sweden_south", "sweden_north" });
            AddProvince("sweden_south", "Gothenburg", FactionType.Sweden, new Vector2(0.41f, 0.78f),
                new[] { "denmark", "norway", "sweden_north", "sweden_east" });
            AddProvince("sweden_east", "Stockholm", FactionType.Sweden, new Vector2(0.46f, 0.80f),
                new[] { "sweden_south", "sweden_north", "finland" });
            AddProvince("sweden_north", "Umeå", FactionType.Sweden, new Vector2(0.44f, 0.86f),
                new[] { "norway", "sweden_south", "sweden_east", "finland" });
            AddProvince("finland", "Helsingfors", FactionType.Sweden, new Vector2(0.54f, 0.82f),
                new[] { "sweden_north", "sweden_east", "karelia", "ingria" });
            
            // ===================== RUSSIAN EMPIRE & POLAND =====================
            AddProvince("poland", "Warsaw", FactionType.Poland, new Vector2(0.52f, 0.64f),
                new[] { "silesia", "posen", "west_prussia", "east_prussia", "lithuania", "volhynia", "galicia" });
            AddProvince("courland", "Mitau", FactionType.Poland, new Vector2(0.58f, 0.74f),
                new[] { "east_prussia", "livonia", "lithuania" });
            AddProvince("livonia", "Riga", FactionType.Russia, new Vector2(0.62f, 0.77f),
                new[] { "courland", "estonia", "pskov", "lithuania" });
            AddProvince("estonia", "Reval", FactionType.Russia, new Vector2(0.62f, 0.80f),
                new[] { "livonia", "ingria", "pskov" });
            AddProvince("ingria", "St. Petersburg", FactionType.Russia, new Vector2(0.66f, 0.82f),
                new[] { "finland", "karelia", "novgorod", "pskov", "estonia" });
            AddProvince("karelia", "Vyborg", FactionType.Russia, new Vector2(0.64f, 0.87f),
                new[] { "finland", "ingria", "novgorod", "olonet" });
            AddProvince("olonet", "Petrozavodsk", FactionType.Russia, new Vector2(0.70f, 0.87f),
                new[] { "karelia", "novgorod", "vologda" });
            AddProvince("vologda", "Vologda", FactionType.Russia, new Vector2(0.76f, 0.82f),
                new[] { "olonet", "novgorod", "kostroma", "yaroslavl" });
            AddProvince("novgorod", "Novgorod", FactionType.Russia, new Vector2(0.70f, 0.78f),
                new[] { "ingria", "karelia", "olonet", "vologda", "tver", "pskov" });
            AddProvince("pskov", "Pskov", FactionType.Russia, new Vector2(0.65f, 0.75f),
                new[] { "estonia", "livonia", "vitebsk", "smolensk", "tver", "novgorod" });
            AddProvince("tver", "Tver", FactionType.Russia, new Vector2(0.72f, 0.74f),
                new[] { "novgorod", "yaroslavl", "vladimir", "moscow", "smolensk", "pskov" });
            AddProvince("yaroslavl", "Yaroslavl", FactionType.Russia, new Vector2(0.77f, 0.76f),
                new[] { "vologda", "kostroma", "vladimir", "tver" });
            AddProvince("kostroma", "Kostroma", FactionType.Russia, new Vector2(0.81f, 0.77f),
                new[] { "vologda", "vyatka", "nizhny_novgorod", "vladimir", "yaroslavl" });
            AddProvince("vyatka", "Vyatka", FactionType.Russia, new Vector2(0.88f, 0.75f),
                new[] { "kostroma", "kazan", "perm" });
            AddProvince("perm", "Perm", FactionType.Russia, new Vector2(0.94f, 0.72f),
                new[] { "vyatka", "kazan", "ufa" });
            AddProvince("vladimir", "Vladimir", FactionType.Russia, new Vector2(0.78f, 0.71f),
                new[] { "yaroslavl", "kostroma", "nizhny_novgorod", "ryazan", "moscow", "tver" });
            AddProvince("nizhny_novgorod", "Nizhny Novgorod", FactionType.Russia, new Vector2(0.83f, 0.71f),
                new[] { "kostroma", "vyatka", "kazan", "simbirsk", "ryazan", "vladimir" });
            AddProvince("kazan", "Kazan", FactionType.Russia, new Vector2(0.89f, 0.69f),
                new[] { "vyatka", "perm", "ufa", "simbirsk", "nizhny_novgorod" });
            AddProvince("ufa", "Ufa", FactionType.Russia, new Vector2(0.94f, 0.65f),
                new[] { "perm", "orenburg", "kazan" });
            AddProvince("moscow", "Moscow", FactionType.Russia, new Vector2(0.75f, 0.68f),
                new[] { "tver", "vladimir", "ryazan", "tula", "kaluga", "smolensk" });
            AddProvince("ryazan", "Ryazan", FactionType.Russia, new Vector2(0.79f, 0.66f),
                new[] { "vladimir", "nizhny_novgorod", "simbirsk", "penza", "tambov", "tula", "moscow" });
            AddProvince("smolensk", "Smolensk", FactionType.Russia, new Vector2(0.68f, 0.69f),
                new[] { "pskov", "tver", "moscow", "kaluga", "orel", "chernigov", "mogilev", "vitebsk" });
            AddProvince("kaluga", "Kaluga", FactionType.Russia, new Vector2(0.73f, 0.65f),
                new[] { "moscow", "tula", "orel", "smolensk" });
            AddProvince("tula", "Tula", FactionType.Russia, new Vector2(0.76f, 0.64f),
                new[] { "moscow", "ryazan", "tambov", "voronezh", "orel", "kaluga" });
            AddProvince("orel", "Orel", FactionType.Russia, new Vector2(0.73f, 0.61f),
                new[] { "kaluga", "tula", "voronezh", "kursk", "chernigov", "smolensk" });
            AddProvince("tambov", "Tambov", FactionType.Russia, new Vector2(0.81f, 0.61f),
                new[] { "ryazan", "penza", "saratov", "voronezh", "tula" });
            AddProvince("penza", "Penza", FactionType.Russia, new Vector2(0.84f, 0.63f),
                new[] { "simbirsk", "saratov", "tambov", "ryazan" });
            AddProvince("simbirsk", "Simbirsk", FactionType.Russia, new Vector2(0.86f, 0.65f),
                new[] { "kazan", "samara", "saratov", "penza", "ryazan", "nizhny_novgorod" });
            AddProvince("samara", "Samara", FactionType.Russia, new Vector2(0.90f, 0.62f),
                new[] { "kazan", "ufa", "orenburg", "saratov", "simbirsk" });
            AddProvince("orenburg", "Orenburg", FactionType.Russia, new Vector2(0.94f, 0.58f),
                new[] { "ufa", "samara" });
            AddProvince("saratov", "Saratov", FactionType.Russia, new Vector2(0.86f, 0.57f),
                new[] { "simbirsk", "samara", "astrakhan", "voronezh", "tambov", "penza" });
            AddProvince("voronezh", "Voronezh", FactionType.Russia, new Vector2(0.78f, 0.57f),
                new[] { "tula", "tambov", "saratov", "don_host", "kharkov", "kursk", "orel" });
            AddProvince("kursk", "Kursk", FactionType.Russia, new Vector2(0.74f, 0.57f),
                new[] { "orel", "voronezh", "kharkov", "poltava", "chernigov" });
            AddProvince("kharkov", "Kharkov", FactionType.Russia, new Vector2(0.76f, 0.53f),
                new[] { "kursk", "voronezh", "don_host", "yekaterinoslav", "poltava" });
            AddProvince("don_host", "Cherkassk", FactionType.Russia, new Vector2(0.82f, 0.52f),
                new[] { "voronezh", "saratov", "astrakhan", "caucasus", "taurida", "yekaterinoslav", "kharkov" });
            AddProvince("astrakhan", "Astrakhan", FactionType.Russia, new Vector2(0.90f, 0.49f),
                new[] { "saratov", "caucasus", "don_host" });
            AddProvince("caucasus", "Stavropol", FactionType.Russia, new Vector2(0.88f, 0.42f),
                new[] { "don_host", "astrakhan", "georgia", "circassia" });
            AddProvince("georgia", "Tiflis", FactionType.Russia, new Vector2(0.92f, 0.38f),
                new[] { "caucasus", "circassia", "armenia", "azerbaijan" });
            AddProvince("circassia", "Ekaterinodar", FactionType.Ottoman, new Vector2(0.85f, 0.41f),
                new[] { "don_host", "caucasus", "georgia", "abkhazia" });
            AddProvince("taurida", "Simferopol", FactionType.Ottoman, new Vector2(0.74f, 0.44f),
                new[] { "yekaterinoslav", "don_host", "crimea", "kherson" });
            AddProvince("crimea", "Sevastopol", FactionType.Ottoman, new Vector2(0.73f, 0.42f),
                new[] { "taurida" });
            AddProvince("yekaterinoslav", "Yekaterinoslav", FactionType.Russia, new Vector2(0.74f, 0.49f),
                new[] { "poltava", "kharkov", "don_host", "taurida", "kherson" });
            AddProvince("kherson", "Kherson", FactionType.Ottoman, new Vector2(0.69f, 0.47f),
                new[] { "kiev", "poltava", "yekaterinoslav", "taurida", "bessarabia", "podolia" });
            AddProvince("poltava", "Poltava", FactionType.Russia, new Vector2(0.71f, 0.53f),
                new[] { "chernigov", "kursk", "kharkov", "yekaterinoslav", "kherson", "kiev" });
            AddProvince("kiev", "Kiev", FactionType.Poland, new Vector2(0.66f, 0.54f),
                new[] { "volhynia", "chernigov", "poltava", "kherson", "podolia" });
            AddProvince("chernigov", "Chernigov", FactionType.Russia, new Vector2(0.68f, 0.58f),
                new[] { "mogilev", "orel", "kursk", "poltava", "kiev", "volhynia" });
            AddProvince("vitebsk", "Vitebsk", FactionType.Poland, new Vector2(0.63f, 0.69f),
                new[] { "livonia", "pskov", "smolensk", "mogilev", "minsk", "lithuania" });
            AddProvince("mogilev", "Mogilev", FactionType.Poland, new Vector2(0.65f, 0.64f),
                new[] { "vitebsk", "smolensk", "chernigov", "minsk" });
            AddProvince("minsk", "Minsk", FactionType.Poland, new Vector2(0.60f, 0.64f),
                new[] { "lithuania", "vitebsk", "mogilev", "volhynia", "grodno" });
            AddProvince("lithuania", "Vilna", FactionType.Poland, new Vector2(0.57f, 0.68f),
                new[] { "courland", "livonia", "vitebsk", "minsk", "grodno", "poland", "east_prussia" });
            AddProvince("grodno", "Grodno", FactionType.Poland, new Vector2(0.56f, 0.64f),
                new[] { "lithuania", "minsk", "volhynia", "poland" });
            AddProvince("volhynia", "Zhitomir", FactionType.Poland, new Vector2(0.59f, 0.59f),
                new[] { "poland", "grodno", "minsk", "kiev", "podolia", "galicia" });
            AddProvince("podolia", "Kamenets", FactionType.Poland, new Vector2(0.62f, 0.54f),
                new[] { "galicia", "volhynia", "kiev", "kherson", "bessarabia", "moldavia" });
            
            // ===================== OTTOMAN EMPIRE & BALKANS =====================
            AddProvince("moldavia", "Jassy", FactionType.Ottoman, new Vector2(0.62f, 0.47f),
                new[] { "transylvania", "podolia", "bessarabia", "wallachia" });
            AddProvince("wallachia", "Bucharest", FactionType.Ottoman, new Vector2(0.57f, 0.44f),
                new[] { "banat", "transylvania", "moldavia", "bulgaria", "serbia" });
            AddProvince("serbia", "Belgrade", FactionType.Ottoman, new Vector2(0.52f, 0.44f),
                new[] { "bosnia", "slavonia", "banat", "wallachia", "bulgaria", "rumelia", "albania" });
            AddProvince("bosnia", "Sarajevo", FactionType.Ottoman, new Vector2(0.48f, 0.42f),
                new[] { "dalmatia", "croatia", "slavonia", "serbia", "albania" });
            AddProvince("bulgaria", "Sofia", FactionType.Ottoman, new Vector2(0.58f, 0.40f),
                new[] { "serbia", "wallachia", "rumelia", "thrace" });
            AddProvince("rumelia", "Monastir", FactionType.Ottoman, new Vector2(0.55f, 0.36f),
                new[] { "albania", "serbia", "bulgaria", "thrace", "macedonia", "epirus" });
            AddProvince("albania", "Janina", FactionType.Ottoman, new Vector2(0.51f, 0.35f),
                new[] { "bosnia", "serbia", "rumelia", "epirus" });
            AddProvince("thrace", "Constantinople", FactionType.Ottoman, new Vector2(0.62f, 0.38f),
                new[] { "bulgaria", "rumelia", "macedonia", "bithynia" });
            AddProvince("macedonia", "Salonica", FactionType.Ottoman, new Vector2(0.57f, 0.33f),
                new[] { "rumelia", "thrace", "thessaly", "epirus" });
            AddProvince("epirus", "Arta", FactionType.Ottoman, new Vector2(0.53f, 0.31f),
                new[] { "albania", "rumelia", "macedonia", "thessaly", "morea" });
            AddProvince("thessaly", "Larissa", FactionType.Ottoman, new Vector2(0.54f, 0.31f),
                new[] { "epirus", "macedonia", "morea" });
            AddProvince("morea", "Athens", FactionType.Ottoman, new Vector2(0.52f, 0.28f),
                new[] { "epirus", "thessaly", "crete" });
            AddProvince("crete", "Candia", FactionType.Ottoman, new Vector2(0.62f, 0.16f),
                new[] { "morea", "rhodes" });
            AddProvince("rhodes", "Rhodes", FactionType.Ottoman, new Vector2(0.67f, 0.23f),
                new[] { "crete", "anatolia", "cyprus" });
            AddProvince("cyprus", "Nicosia", FactionType.Ottoman, new Vector2(0.71f, 0.22f),
                new[] { "rhodes", "cilicia", "syria" });
            
            // Anatolia and Middle East
            AddProvince("bithynia", "Bursa", FactionType.Ottoman, new Vector2(0.66f, 0.36f),
                new[] { "thrace", "anatolia", "paphlagonia" });
            AddProvince("anatolia", "Smyrna", FactionType.Ottoman, new Vector2(0.65f, 0.30f),
                new[] { "rhodes", "bithynia", "paphlagonia", "karaman" });
            AddProvince("paphlagonia", "Angora", FactionType.Ottoman, new Vector2(0.72f, 0.35f),
                new[] { "bithynia", "anatolia", "karaman", "pontus" });
            AddProvince("karaman", "Konya", FactionType.Ottoman, new Vector2(0.71f, 0.30f),
                new[] { "anatolia", "paphlagonia", "pontus", "cilicia" });
            AddProvince("pontus", "Trebizond", FactionType.Ottoman, new Vector2(0.78f, 0.36f),
                new[] { "paphlagonia", "karaman", "cilicia", "armenia_minor", "armenia" });
            AddProvince("cilicia", "Adana", FactionType.Ottoman, new Vector2(0.75f, 0.26f),
                new[] { "cyprus", "karaman", "pontus", "armenia_minor", "syria" });
            AddProvince("armenia_minor", "Sivas", FactionType.Ottoman, new Vector2(0.80f, 0.30f),
                new[] { "pontus", "cilicia", "syria", "armenia", "mesopotamia" });
            AddProvince("armenia", "Erzurum", FactionType.Ottoman, new Vector2(0.86f, 0.34f),
                new[] { "georgia", "pontus", "armenia_minor", "mesopotamia", "kurdistan" });
            AddProvince("kurdistan", "Diyarbekir", FactionType.Ottoman, new Vector2(0.87f, 0.28f),
                new[] { "armenia", "mesopotamia", "iraq", "persia" });
            AddProvince("mesopotamia", "Mosul", FactionType.Ottoman, new Vector2(0.85f, 0.24f),
                new[] { "syria", "armenia_minor", "armenia", "kurdistan", "iraq", "arabia" });
            AddProvince("syria", "Aleppo", FactionType.Ottoman, new Vector2(0.75f, 0.25f),
                new[] { "cyprus", "cilicia", "armenia_minor", "mesopotamia", "lebanon", "arabia" });
            AddProvince("lebanon", "Damascus", FactionType.Ottoman, new Vector2(0.76f, 0.14f),
                new[] { "syria", "palestine", "arabia" });
            AddProvince("palestine", "Jerusalem", FactionType.Ottoman, new Vector2(0.74f, 0.13f),
                new[] { "lebanon", "egypt", "arabia" });
            AddProvince("egypt", "Cairo", FactionType.Ottoman, new Vector2(0.70f, 0.13f),
                new[] { "palestine", "arabia", "cyrenaica" }); // Ottoman controlled by 1805
            AddProvince("arabia", "Medina", FactionType.Ottoman, new Vector2(0.81f, 0.13f),
                new[] { "palestine", "lebanon", "syria", "mesopotamia", "iraq" });
            AddProvince("iraq", "Baghdad", FactionType.Ottoman, new Vector2(0.89f, 0.16f),
                new[] { "arabia", "mesopotamia", "kurdistan", "persia" });
            
            // ===================== NORTH AFRICA =====================
            AddProvince("cyrenaica", "Benghazi", FactionType.Ottoman, new Vector2(0.60f, 0.13f),
                new[] { "egypt", "tripolitania" });
            AddProvince("tripolitania", "Tripoli", FactionType.Ottoman, new Vector2(0.49f, 0.13f),
                new[] { "cyrenaica", "tunis" });
            AddProvince("tunis", "Tunis", FactionType.Ottoman, new Vector2(0.42f, 0.14f),
                new[] { "tripolitania", "sicily", "algiers" });
            AddProvince("algiers", "Algiers", FactionType.Ottoman, new Vector2(0.30f, 0.22f),
                new[] { "tunis", "oran" });
            AddProvince("oran", "Oran", FactionType.Ottoman, new Vector2(0.26f, 0.25f),
                new[] { "algiers", "morocco" });
            AddProvince("morocco", "Fes", FactionType.Ottoman, new Vector2(0.12f, 0.22f), // Independent Sultanate, Ottoman sphere
                new[] { "oran", "andalusia", "gibraltar" });

            // Assign provinces to factions
            foreach (var kvp in provinces)
            {
                FactionType owner = kvp.Value.owner;
                if (factions.ContainsKey(owner))
                    factions[owner].ownedProvinceIds.Add(kvp.Key);
            }

            AssignProvinceTerrainTypes();
            AssignProvinceCulturesAndReligions();
            
            // Apply saved position overrides (from city drag editing)
            CityPositionOverrides.ApplyOverrides(provinces);
        }

        private void AssignProvinceTerrainTypes()
        {
            // Mountains
            string[] mountains = { "tyrol", "salzburg", "carinthia", "transylvania", "caucasus",
                "georgia", "circassia", "armenia", "kurdistan", "pontus", "switzerland", "highlands" };
            // Hills
            string[] hills = { "bohemia", "moravia", "galicia", "styria", "illyria",
                "scotland", "wales", "aragon", "catalonia", "asturias", "thessaly",
                "epirus", "albania", "rumelia", "karaman", "paphlagonia", "upper_austria", "cumbria", "yorkshire" };
            // Forest
            string[] forest = { "saxony", "bavaria", "westphalia", "hannover", "franconia",
                "thuringia", "silesia", "brandenburg", "pomerania", "mecklenburg",
                "east_prussia", "west_prussia", "lithuania", "grodno", "minsk",
                "vitebsk", "mogilev", "sweden_north", "norway", "karelia", "olonet" };
            // Snow/Tundra
            string[] snow = { "finland", "vologda", "perm", "ufa", "orenburg",
                "vyatka", "kostroma" };
            // Coastal
            string[] coastal = { "normandy", "brittany", "holland", "belgium", "denmark",
                "sweden_south", "sweden_east", "dalmatia", "naples", "sicily", "sardinia",
                "crete", "rhodes", "cyprus", "crimea", "corsica", "gibraltar",
                "morea", "valencia", "andalusia", "portugal", "galicia_spain",
                "cornwall", "kent", "east_anglia", "ireland", "west_ireland" };
            // Desert/Arid
            string[] desert = { "egypt", "arabia", "palestine", "syria", "lebanon",
                "mesopotamia", "iraq", "cyrenaica", "tripolitania", "tunis", "algiers",
                "oran", "morocco", "astrakhan" };
            // Marsh
            string[] marsh = { "holland", "courland", "livonia", "estonia",
                "kherson", "bessarabia", "moldavia" };
            // Urban (major capitals)
            string[] urban = { "paris", "london", "vienna", "berlin",
                "constantinople", "moscow", "madrid", "rome",
                "amsterdam", "warsaw" };
            // Plains = default for everything else

            foreach (var kvp in provinces)
            {
                string id = kvp.Key;
                var prov = kvp.Value;

                // Priority: Urban > Mountains > Desert > Snow > Coastal > Marsh > Forest > Hills > Plains
                if (System.Array.IndexOf(urban, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Urban;
                else if (System.Array.IndexOf(mountains, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Mountains;
                else if (System.Array.IndexOf(desert, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Desert;
                else if (System.Array.IndexOf(snow, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Snow;
                else if (System.Array.IndexOf(coastal, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Coastal;
                else if (System.Array.IndexOf(marsh, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Marsh;
                else if (System.Array.IndexOf(forest, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Forest;
                else if (System.Array.IndexOf(hills, id) >= 0)
                    prov.terrainType = ProvinceTerrainType.Hills;
                else
                    prov.terrainType = ProvinceTerrainType.Plains;
            }

            Debug.Log($"[CampaignManager] Assigned terrain types to {provinces.Count} provinces");
        }

        private void AssignProvinceCulturesAndReligions()
        {
            // === CULTURES ===
            // French
            string[] french = { "paris", "normandy", "brittany", "picardy", "champagne", "orleans",
                "burgundy", "aquitaine", "auvergne", "lyon", "toulouse", "languedoc", "dauphine",
                "provence", "franche_comte", "lorraine", "nice" };
            // Occitan
            string[] occitan = { "toulouse", "languedoc", "provence", "dauphine" };
            // Breton
            string[] breton = { "brittany" };
            // German
            string[] german = { "hannover", "westphalia", "franconia", "thuringia", "wurttemberg",
                "baden", "palatinate", "hesse", "rhineland" };
            // Prussian
            string[] prussian = { "berlin", "brandenburg", "pomerania", "east_prussia", "west_prussia",
                "silesia", "mecklenburg" };
            // Austrian
            string[] austrian = { "vienna", "upper_austria", "styria", "carinthia", "tyrol", "salzburg" };
            // Bavarian
            string[] bavarian = { "bavaria", "munich" };
            // Saxon
            string[] saxon = { "saxony", "dresden" };
            // British
            string[] british = { "london", "kent", "east_anglia", "cornwall", "midlands", "yorkshire",
                "cumbria", "scotland", "highlands", "wales", "ireland", "west_ireland", "gibraltar" };
            // Spanish
            string[] spanish = { "madrid", "castile", "leon", "andalusia", "murcia", "valencia",
                "galicia_spain", "asturias", "extremadura", "la_mancha" };
            // Catalan
            string[] catalan = { "catalonia", "aragon", "balearic" };
            // Basque
            string[] basque = { "navarre" };
            // Portuguese
            string[] portuguese = { "portugal", "lisbon", "algarve" };
            // Italian
            string[] italian = { "piedmont", "lombardy", "venetia", "genoa", "tuscany_prov", "rome",
                "naples", "sicily", "sardinia", "corsica", "modena_prov", "parma_prov", "savoy",
                "nice", "papal_states_prov", "romagna", "umbria", "calabria", "apulia" };
            // Dutch
            string[] dutch = { "holland", "flanders", "brabant" };
            // Polish
            string[] polish = { "warsaw", "krakow", "poznan", "lublin", "podlasie",
                "galicia", "mazovia" };
            // Russian
            string[] russian = { "moscow", "st_petersburg", "novgorod", "pskov", "tver", "smolensk",
                "kaluga", "ryazan", "vladimir", "nizhny_novgorod", "kazan", "vologda", "kostroma",
                "yaroslavl", "tambov", "voronezh", "kursk", "orel", "perm", "vyatka", "ufa",
                "orenburg", "saratov", "penza", "simbirsk", "astrakhan", "don", "kuban",
                "kiev", "chernigov", "poltava", "kharkov", "ekaterinoslav", "kherson",
                "crimea", "taurida", "olonet", "arkhangelsk", "karelia" };
            // Swedish
            string[] swedish = { "sweden_south", "sweden_east", "sweden_north", "gotland", "stockholm" };
            // Danish
            string[] danish = { "denmark", "jutland", "sjaelland", "norway", "iceland" };
            // Turkish
            string[] turkish = { "constantinople", "anatolia", "paphlagonia", "pontus", "karaman",
                "smyrna", "ankara", "adana", "sivas" };
            // Greek
            string[] greek = { "morea", "athens", "thessaly", "epirus", "crete", "rhodes", "cyprus",
                "aegean_islands" };
            // Hungarian
            string[] hungarian = { "hungary", "pressburg", "transylvania" };
            // Czech
            string[] czech = { "bohemia", "moravia" };
            // Croatian
            string[] croatian = { "croatia", "dalmatia", "illyria", "slavonia" };
            // Arab
            string[] arab = { "egypt", "palestine", "syria", "lebanon", "mesopotamia", "iraq",
                "arabia", "hejaz", "yemen" };

            // Apply cultures
            void SetCulture(string[] ids, CultureType culture)
            {
                foreach (string id in ids)
                    if (provinces.ContainsKey(id)) provinces[id].primaryCulture = culture;
            }

            // Default: use faction culture
            foreach (var kvp in provinces)
                kvp.Value.primaryCulture = ProvinceData.GetFactionCulture(kvp.Value.owner);

            // Override with specific cultures (more specific overrides less specific)
            SetCulture(french, CultureType.French);
            SetCulture(occitan, CultureType.Occitan);
            SetCulture(breton, CultureType.Breton);
            SetCulture(german, CultureType.German);
            SetCulture(prussian, CultureType.Prussian);
            SetCulture(austrian, CultureType.Austrian);
            SetCulture(bavarian, CultureType.Bavarian);
            SetCulture(saxon, CultureType.Saxon);
            SetCulture(british, CultureType.British);
            SetCulture(spanish, CultureType.Spanish);
            SetCulture(catalan, CultureType.Catalan);
            SetCulture(basque, CultureType.Basque);
            SetCulture(portuguese, CultureType.Portuguese);
            SetCulture(italian, CultureType.Italian);
            SetCulture(dutch, CultureType.Dutch);
            SetCulture(polish, CultureType.Polish);
            SetCulture(russian, CultureType.Russian);
            SetCulture(swedish, CultureType.Swedish);
            SetCulture(danish, CultureType.Danish);
            SetCulture(turkish, CultureType.Turkish);
            SetCulture(greek, CultureType.Greek);
            SetCulture(hungarian, CultureType.Hungarian);
            SetCulture(czech, CultureType.Czech);
            SetCulture(croatian, CultureType.Croatian);
            SetCulture(arab, CultureType.Arab);

            // === RELIGIONS ===
            // Default: use faction religion
            foreach (var kvp in provinces)
                kvp.Value.primaryReligion = ProvinceData.GetFactionReligion(kvp.Value.owner);

            // Override specific regions
            void SetReligion(string[] ids, ReligionType religion)
            {
                foreach (string id in ids)
                    if (provinces.ContainsKey(id)) provinces[id].primaryReligion = religion;
            }

            // Orthodox regions
            string[] orthodox = { "moscow", "st_petersburg", "novgorod", "pskov", "tver", "smolensk",
                "kaluga", "ryazan", "vladimir", "nizhny_novgorod", "kazan", "vologda", "kostroma",
                "yaroslavl", "tambov", "voronezh", "kursk", "orel", "perm", "vyatka", "ufa",
                "orenburg", "saratov", "astrakhan", "don", "kuban", "kiev", "chernigov",
                "poltava", "kharkov", "ekaterinoslav", "kherson", "crimea", "taurida",
                "olonet", "arkhangelsk", "karelia", "morea", "athens", "thessaly", "epirus",
                "crete", "aegean_islands", "moldavia", "wallachia", "serbia", "montenegro",
                "georgia", "armenia" };
            SetReligion(orthodox, ReligionType.Orthodox);

            // Islamic regions
            string[] islamic = { "constantinople", "anatolia", "paphlagonia", "pontus", "karaman",
                "smyrna", "ankara", "adana", "sivas", "egypt", "palestine", "syria", "lebanon",
                "mesopotamia", "iraq", "arabia", "hejaz", "yemen", "cyrenaica", "tripolitania",
                "tunis", "algiers", "oran", "morocco", "albania", "rumelia", "kurdistan",
                "circassia", "caucasus" };
            SetReligion(islamic, ReligionType.Islamic);

            // Protestant regions
            string[] protestant = { "berlin", "brandenburg", "pomerania", "east_prussia", "west_prussia",
                "silesia", "mecklenburg", "saxony", "hannover", "westphalia", "hesse",
                "thuringia", "sweden_south", "sweden_east", "sweden_north", "denmark",
                "norway", "finland", "jutland" };
            SetReligion(protestant, ReligionType.Protestant);

            // Anglican
            SetReligion(british, ReligionType.Anglican);

            // Reformed
            string[] reformed = { "holland", "switzerland", "geneva" };
            SetReligion(reformed, ReligionType.Reformed);

            // Coptic
            string[] coptic = { "egypt" }; // Coptic minority in Egypt
            // Egypt stays Islamic as majority, keep it

            // Set initial state for all provinces
            foreach (var kvp in provinces)
            {
                var prov = kvp.Value;
                prov.originalOwner = prov.owner;
                prov.isCored = true;
                prov.isOccupied = false;
                prov.loyalty = 80f;
                prov.unrest = 0f;
                prov.prosperity = 50f;
                prov.devastation = 0f;

                // Provinces with mismatched culture start with some unrest
                if (!prov.IsCultureAccepted) prov.unrest = 10f;
                if (!prov.IsReligionAccepted) prov.unrest += 5f;
            }

            Debug.Log($"[CampaignManager] Assigned cultures and religions to {provinces.Count} provinces");
        }

        private void AddProvince(string id, string name, FactionType owner, Vector2 position, string[] neighbors)
        {
            // Apply scale and offset to match the new heightmap image projection
            Vector2 alignedPosition = (position * mapScale) + mapOffset;
            
            var prov = new ProvinceData(name, id, owner, alignedPosition, neighbors);
            provinces.Add(id, prov);
        }

        private void CreateCities()
        {
            // Initialize production chain manager
            ProductionChainManager.Instance.Initialize();
            
            // Initialize logistics convoy system
            LogisticsConvoySystem.Initialize();
            
            // Create major cities for each province
            foreach (var kvp in provinces)
            {
                ProvinceData prov = kvp.Value;
                string cityId = $"city_{prov.provinceId}";
                
                // Create city data with slightly offset position
                Vector2 cityPos = prov.mapPosition + new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f));
                CityData city = new CityData(cityId, prov.provinceName, prov.provinceId, cityPos, prov.owner);
                
                // Add special industries based on province characteristics
                SetupCityIndustries(city, prov.provinceId);
                
                cities[cityId] = city;
                prov.population = city.population; // Sync population
            }
            
            Debug.Log($"[CampaignManager] Created {cities.Count} cities");
        }
        
        private void SetupCityIndustries(CityData city, string provinceId)
        {
            // Major Capital Cities - Large populations, commerce & manufacturing
            string[] capitals = { "paris", "london", "berlin", "vienna", "moscow", "madrid", "constantinople", 
                "st_petersburg", "warsaw", "budapest", "rome", "amsterdam" };
            
            // Industrial Regions - Mining & manufacturing
            string[] industrial = { "rhineland", "silesia", "bohemia", "moravia", "saxony", "westphalia", 
                "hannover", "brandenburg", "pomerania", "lombardy", "belgium", "galicia" };
            
            // Port Cities - Commerce & fishing
            string[] ports = { "bordeaux", "marseille", "barcelona", "genoa", "venice", "naples", "tunis",
                "trieste", "copenhagen", "stockholm", "oslo", "helsinki", "riga", "tallinn", "lisbon",
                "seville", "gibraltar", "algiers", "alexandria", "constantinople_port", "odessa" };
            
            // Agricultural Regions
            string[] agricultural = { "normandy", "brittany", "orleans", "loire", "champagne", "picardy",
                "burgundy", "toulouse", "pyrenees", "castile", "navarre", "aragon", "catalonia",
                "portugal", "ukraine", "anatolia", "egypt", "wallachia", "transylvania", "moldavia" };
            
            // Military Frontier / Garrison regions
            string[] military = { "savoy", "franche_comte", "nice", "switzerland", "piedmont", "corsica",
                "sardinia", "sicily", "croatia", "bosnia", "serbia", "albania", "greece", "bulgaria",
                "adrianople", "morea", "syria", "konya", "barbary", "tripoli", "crimea" };
            
            if (System.Array.Exists(capitals, x => x == provinceId))
            {
                city.isCapital = true;
                city.cityLevel = 3; // Capitals start as "Ville"
                
                // Each capital gets unique, historically-inspired setup
                switch (provinceId)
                {
                    case "paris":
                        city.AddIndustry(IndustryType.Commerce, 4);
                        city.AddIndustry(IndustryType.Manufacturing, 3);
                        city.AddIndustry(IndustryType.Textile, 2);
                        city.populationData = PopulationData.FromTotal(550000);
                        city.maxPopulation = 1200000;
                        city.storedFood = 600f; city.storedIron = 200f; city.storedGoods = 300f;
                        city.maxGarrison = 3000;
                        break;
                    case "london":
                        city.AddIndustry(IndustryType.Commerce, 3);
                        city.AddIndustry(IndustryType.Manufacturing, 3);
                        city.AddIndustry(IndustryType.Fishing, 2);
                        city.AddIndustry(IndustryType.Shipbuilding, 2);
                        city.populationData = PopulationData.FromTotal(900000);
                        city.maxPopulation = 1500000;
                        city.storedFood = 400f; city.storedIron = 500f; city.storedGoods = 400f;
                        city.maxGarrison = 2500;
                        break;
                    case "moscow":
                        city.AddIndustry(IndustryType.Agriculture, 4);
                        city.AddIndustry(IndustryType.Commerce, 2);
                        city.AddIndustry(IndustryType.Logging, 3);
                        city.populationData = PopulationData.FromTotal(250000);
                        city.maxPopulation = 800000;
                        city.storedFood = 1000f; city.storedIron = 100f; city.storedGoods = 100f;
                        city.maxGarrison = 4000;
                        break;
                    case "vienna":
                    case "lower_austria":
                        city.AddIndustry(IndustryType.Commerce, 3);
                        city.AddIndustry(IndustryType.Manufacturing, 2);
                        city.AddIndustry(IndustryType.Textile, 2);
                        city.populationData = PopulationData.FromTotal(230000);
                        city.maxPopulation = 600000;
                        city.storedFood = 350f; city.storedIron = 250f; city.storedGoods = 200f;
                        city.maxGarrison = 2500;
                        break;
                    case "berlin":
                    case "brandenburg":
                        city.AddIndustry(IndustryType.Manufacturing, 3);
                        city.AddIndustry(IndustryType.Mining, 2);
                        city.AddIndustry(IndustryType.Commerce, 2);
                        city.populationData = PopulationData.FromTotal(170000);
                        city.maxPopulation = 500000;
                        city.storedFood = 200f; city.storedIron = 400f; city.storedGoods = 150f;
                        city.maxGarrison = 3500;
                        break;
                    case "madrid":
                    case "castile":
                        city.AddIndustry(IndustryType.Commerce, 3);
                        city.AddIndustry(IndustryType.Agriculture, 2);
                        city.AddIndustry(IndustryType.Mining, 1);
                        city.populationData = PopulationData.FromTotal(160000);
                        city.maxPopulation = 400000;
                        city.storedFood = 300f; city.storedIron = 150f; city.storedGoods = 350f;
                        city.maxGarrison = 2000;
                        break;
                    case "constantinople":
                    case "thrace":
                        city.AddIndustry(IndustryType.Commerce, 4);
                        city.AddIndustry(IndustryType.Fishing, 2);
                        city.AddIndustry(IndustryType.Textile, 2);
                        city.populationData = PopulationData.FromTotal(400000);
                        city.maxPopulation = 700000;
                        city.storedFood = 450f; city.storedIron = 100f; city.storedGoods = 500f;
                        city.maxGarrison = 3000;
                        break;
                    case "st_petersburg":
                        city.AddIndustry(IndustryType.Commerce, 2);
                        city.AddIndustry(IndustryType.Shipbuilding, 2);
                        city.AddIndustry(IndustryType.Manufacturing, 2);
                        city.populationData = PopulationData.FromTotal(220000);
                        city.maxPopulation = 500000;
                        city.storedFood = 250f; city.storedIron = 300f; city.storedGoods = 200f;
                        city.maxGarrison = 2500;
                        break;
                    default:
                        // Other capitals — generic strong setup
                        city.AddIndustry(IndustryType.Commerce, 3);
                        city.AddIndustry(IndustryType.Manufacturing, 2);
                        city.populationData = PopulationData.FromTotal(120000);
                        city.maxPopulation = 500000;
                        city.storedFood = 300f; city.storedIron = 200f; city.storedGoods = 150f;
                        city.maxGarrison = 2000;
                        break;
                }
            }
            else if (System.Array.Exists(industrial, x => x == provinceId))
            {
                // Industrial regions
                city.AddIndustry(IndustryType.Mining, 2);
                city.AddIndustry(IndustryType.Manufacturing, 2);
                city.AddIndustry(IndustryType.Commerce, 1);
                city.populationData = PopulationData.FromTotal(60000);
                city.maxPopulation = 400000;
            }
            else if (System.Array.Exists(ports, x => x == provinceId))
            {
                // Port cities - commerce and fishing
                city.AddIndustry(IndustryType.Commerce, 2);
                city.AddIndustry(IndustryType.Fishing, 1);
                city.AddIndustry(IndustryType.Manufacturing, 1);
                city.populationData = PopulationData.FromTotal(45000);
                city.maxPopulation = 350000;
            }
            else if (System.Array.Exists(agricultural, x => x == provinceId))
            {
                // Agricultural regions
                city.AddIndustry(IndustryType.Agriculture, 3);
                city.AddIndustry(IndustryType.Logging, 1);
                city.populationData = PopulationData.FromTotal(35000);
                city.maxPopulation = 250000;
            }
            else if (System.Array.Exists(military, x => x == provinceId))
            {
                // Military frontier regions
                city.AddIndustry(IndustryType.Agriculture, 2);
                city.AddIndustry(IndustryType.Mining, 1);
                city.populationData = PopulationData.FromTotal(30000);
                city.maxPopulation = 200000;
                city.maxGarrison = 1500; // Higher garrison capacity
            }
            else
            {
                // Default setup with basic industries
                city.AddIndustry(IndustryType.Agriculture, 2);
                city.AddIndustry(IndustryType.Logging, 1);
                city.populationData = PopulationData.FromTotal(25000);
                city.maxPopulation = 200000;
            }
            
            // Update city level after setting population
            city.cityLevel = 1;
        }
        
        public CityData GetCityForProvince(string provinceId)
        {
            string cityId = $"city_{provinceId}";
            return cities.ContainsKey(cityId) ? cities[cityId] : null;
        }
        
        public Dictionary<string, ProvinceData> GetAllProvinces() => provinces;
        
        public List<CityData> GetFactionCities(FactionType faction)
        {
            List<CityData> result = new List<CityData>();
            foreach (var city in cities.Values)
            {
                if (city.owner == faction)
                    result.Add(city);
            }
            return result;
        }

        private void CreateStartingArmies()
        {
            // France
            ArmyData frenchMain = new ArmyData("Grande Armee", "fr_main", FactionType.France, "paris");
            frenchMain.AddRegiment(new RegimentData("1st Line Infantry", UnitType.LineInfantry, 60));
            frenchMain.AddRegiment(new RegimentData("2nd Line Infantry", UnitType.LineInfantry, 60));
            frenchMain.AddRegiment(new RegimentData("3rd Line Infantry", UnitType.LineInfantry, 60));
            frenchMain.AddRegiment(new RegimentData("Imperial Guard", UnitType.Grenadier, 40));
            frenchMain.AddRegiment(new RegimentData("1st Cuirassiers", UnitType.Cavalry, 30));
            frenchMain.AddRegiment(new RegimentData("Grand Battery", UnitType.Artillery, 6));
            RegisterArmy(frenchMain);

            ArmyData frenchSouth = new ArmyData("Army of Italy", "fr_south", FactionType.France, "piedmont");
            frenchSouth.AddRegiment(new RegimentData("4th Line Infantry", UnitType.LineInfantry, 60));
            frenchSouth.AddRegiment(new RegimentData("5th Line Infantry", UnitType.LineInfantry, 60));
            frenchSouth.AddRegiment(new RegimentData("2nd Hussars", UnitType.Hussar, 30));
            RegisterArmy(frenchSouth);

            // Britain
            ArmyData britishMain = new ArmyData("British Expeditionary Force", "br_main", FactionType.Britain, "london");
            britishMain.AddRegiment(new RegimentData("1st Foot Guards", UnitType.LineInfantry, 60));
            britishMain.AddRegiment(new RegimentData("2nd Foot Guards", UnitType.LineInfantry, 60));
            britishMain.AddRegiment(new RegimentData("95th Rifles", UnitType.LightInfantry, 40));
            britishMain.AddRegiment(new RegimentData("Royal Horse Guards", UnitType.Cavalry, 30));
            britishMain.AddRegiment(new RegimentData("Royal Artillery", UnitType.Artillery, 6));
            RegisterArmy(britishMain);

            // Prussia
            ArmyData prussianMain = new ArmyData("Prussian Army", "pr_main", FactionType.Prussia, "brandenburg");
            prussianMain.AddRegiment(new RegimentData("1st Prussian Line", UnitType.LineInfantry, 60));
            prussianMain.AddRegiment(new RegimentData("2nd Prussian Line", UnitType.LineInfantry, 60));
            prussianMain.AddRegiment(new RegimentData("Prussian Grenadiers", UnitType.Grenadier, 40));
            prussianMain.AddRegiment(new RegimentData("Prussian Uhlans", UnitType.Lancer, 30));
            RegisterArmy(prussianMain);

            // Russia
            ArmyData russianMain = new ArmyData("Imperial Russian Army", "ru_main", FactionType.Russia, "moscow");
            russianMain.AddRegiment(new RegimentData("1st Russian Line", UnitType.LineInfantry, 60));
            russianMain.AddRegiment(new RegimentData("2nd Russian Line", UnitType.LineInfantry, 60));
            russianMain.AddRegiment(new RegimentData("3rd Russian Line", UnitType.LineInfantry, 60));
            russianMain.AddRegiment(new RegimentData("Russian Cossacks", UnitType.Cavalry, 30));
            russianMain.AddRegiment(new RegimentData("Russian Artillery", UnitType.Artillery, 6));
            RegisterArmy(russianMain);

            // Austria
            ArmyData austrianMain = new ArmyData("Austrian Army", "au_main", FactionType.Austria, "lower_austria");
            austrianMain.AddRegiment(new RegimentData("1st Austrian Line", UnitType.LineInfantry, 60));
            austrianMain.AddRegiment(new RegimentData("2nd Austrian Line", UnitType.LineInfantry, 60));
            austrianMain.AddRegiment(new RegimentData("Austrian Hussars", UnitType.Hussar, 30));
            austrianMain.AddRegiment(new RegimentData("Austrian Battery", UnitType.Artillery, 6));
            RegisterArmy(austrianMain);

            // Spain
            ArmyData spanishMain = new ArmyData("Spanish Royal Army", "sp_main", FactionType.Spain, "castile");
            spanishMain.AddRegiment(new RegimentData("1st Spanish Line", UnitType.LineInfantry, 60));
            spanishMain.AddRegiment(new RegimentData("2nd Spanish Line", UnitType.LineInfantry, 60));
            spanishMain.AddRegiment(new RegimentData("Spanish Grenadiers", UnitType.Grenadier, 40));
            spanishMain.AddRegiment(new RegimentData("Spanish Dragoons", UnitType.Cavalry, 30));
            spanishMain.AddRegiment(new RegimentData("Spanish Artillery", UnitType.Artillery, 6));
            RegisterArmy(spanishMain);

            ArmyData spanishSouth = new ArmyData("Army of Andalusia", "sp_south", FactionType.Spain, "andalusia");
            spanishSouth.AddRegiment(new RegimentData("3rd Spanish Line", UnitType.LineInfantry, 60));
            spanishSouth.AddRegiment(new RegimentData("Guerrilleros", UnitType.LightInfantry, 40));
            RegisterArmy(spanishSouth);

            // Ottoman Empire
            ArmyData ottomanMain = new ArmyData("Ottoman Imperial Army", "ot_main", FactionType.Ottoman, "thrace");
            ottomanMain.AddRegiment(new RegimentData("Janissary Corps", UnitType.Grenadier, 50));
            ottomanMain.AddRegiment(new RegimentData("1st Nizam Infantry", UnitType.LineInfantry, 60));
            ottomanMain.AddRegiment(new RegimentData("2nd Nizam Infantry", UnitType.LineInfantry, 60));
            ottomanMain.AddRegiment(new RegimentData("Sipahi Cavalry", UnitType.Cavalry, 35));
            ottomanMain.AddRegiment(new RegimentData("Ottoman Artillery", UnitType.Artillery, 8));
            RegisterArmy(ottomanMain);

            ArmyData ottomanEast = new ArmyData("Army of Anatolia", "ot_east", FactionType.Ottoman, "anatolia");
            ottomanEast.AddRegiment(new RegimentData("3rd Nizam Infantry", UnitType.LineInfantry, 60));
            ottomanEast.AddRegiment(new RegimentData("Mameluk Cavalry", UnitType.Hussar, 30));
            ottomanEast.AddRegiment(new RegimentData("Akinci Raiders", UnitType.LightInfantry, 40));
            RegisterArmy(ottomanEast);

            ArmyData ottomanEgypt = new ArmyData("Army of Egypt", "ot_egypt", FactionType.Ottoman, "egypt");
            ottomanEgypt.AddRegiment(new RegimentData("Egyptian Mameluks", UnitType.Cavalry, 40));
            ottomanEgypt.AddRegiment(new RegimentData("Egyptian Infantry", UnitType.LineInfantry, 60));
            RegisterArmy(ottomanEgypt);
        }

        private void RegisterArmy(ArmyData army)
        {
            armies[army.armyId] = army;
            if (factions.ContainsKey(army.faction))
                factions[army.faction].armyIds.Add(army.armyId);
        }

        private void CreateStartingGenerals()
        {
            // France
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Napoléon Bonaparte", FactionType.France, 10, 10, 9));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Louis Davout", FactionType.France, 9, 8, 6));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Michel Ney", FactionType.France, 8, 9, 7));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Jean Lannes", FactionType.France, 8, 7, 8));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("André Masséna", FactionType.France, 7, 6, 5));

            // Britain
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Arthur Wellesley", FactionType.Britain, 9, 8, 7));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("John Moore", FactionType.Britain, 7, 7, 6));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Thomas Graham", FactionType.Britain, 6, 6, 5));

            // Prussia
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Gebhard von Blücher", FactionType.Prussia, 8, 9, 7));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("August von Gneisenau", FactionType.Prussia, 7, 7, 6));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Gerhard von Scharnhorst", FactionType.Prussia, 7, 6, 7));

            // Russia
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Mikhail Kutuzov", FactionType.Russia, 8, 7, 8));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Pyotr Bagration", FactionType.Russia, 8, 8, 6));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Levin Bennigsen", FactionType.Russia, 6, 6, 5));

            // Austria
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Karl von Schwarzenberg", FactionType.Austria, 7, 7, 6));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Archduke Charles", FactionType.Austria, 8, 7, 7));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Johann von Hiller", FactionType.Austria, 6, 5, 5));

            // Spain
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Xavier Castaños", FactionType.Spain, 6, 6, 6));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Pedro Cuesta", FactionType.Spain, 5, 6, 5));

            // Ottoman
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Mustafa Pasha", FactionType.Ottoman, 6, 7, 5));
            RegisterGeneral(GeneralData.CreateHistoricalGeneral("Ahmed Bey", FactionType.Ottoman, 5, 6, 6));

            // Auto-assign first general of each faction to their first army
            foreach (var factionKvp in factions)
            {
                var fType = factionKvp.Key;
                var fData = factionKvp.Value;
                if (fData.armyIds.Count == 0) continue;

                string firstArmyId = fData.armyIds[0];
                foreach (var gen in generals.Values)
                {
                    if (gen.faction == fType && gen.isAlive && string.IsNullOrEmpty(gen.assignedArmyId))
                    {
                        gen.assignedArmyId = firstArmyId;
                        if (armies.ContainsKey(firstArmyId))
                            armies[firstArmyId].generalId = gen.generalId;
                        break;
                    }
                }
            }
        }

        private void RegisterGeneral(GeneralData general)
        {
            generals[general.generalId] = general;
        }

        // === HOI4 SYSTEMS INITIALIZATION ===

        private void InitializeHoI4Systems()
        {
            // Initialize supply system with province data
            SupplySystem.Initialize(provinces);

            // Initialize logistics convoy system
            LogisticsConvoySystem.Initialize();

            foreach (var kvp in factions)
            {
                FactionData fd = kvp.Value;
                FactionType ft = kvp.Key;

                // Initialize starting equipment (150% of current army demand)
                EquipmentSystem.InitializeStartingEquipment(fd, armies);

                // Create default production lines for each faction
                ProductionManager.CreateDefaultProductionLines(fd);

                // Create default division templates
                DivisionDesigner.CreateDefaultTemplates(ft);

                // Initialize economy manager (budget, debt, inflation, extended resources)
                EconomyManager.Initialize(ft);

                // Initialize national laws (8 categories with cooldowns)
                NationalLaws.Initialize(ft);

                // Initialize supply depot at capital (first owned province)
                if (fd.ownedProvinceIds.Count > 0)
                {
                    SupplyLineSystem.Initialize(ft, fd.ownedProvinceIds[0]);
                }
            }

            Debug.Log("[CampaignManager] HoI4 systems initialized (Equipment, Production, Supply, Logistics, Divisions, Economy)");
        }

        // === TURN SYSTEM ===

        /// <summary>
        /// Legacy compatibility — forces an immediate season tick.
        /// </summary>
        public void EndPlayerTurn()
        {
            ProcessSeasonTick();
        }

        /// <summary>
        /// Light daily processing — called every 5 real seconds (= 1 game day).
        /// Handles: date advance, army movement progress, daily UI updates.
        /// </summary>
        public void ProcessDayTick()
        {
            // Advance the calendar by one day
            SeasonSystem.AdvanceDay();
            
            // Advance the day counter
            currentTurn++;
            
            // === Daily production processing for ALL cities ===
            foreach (var city in cities.Values)
            {
                if (city.productionQueue.Count > 0)
                {
                    var item = city.productionQueue[0];
                    item.turnsRemaining--;
                    
                    Debug.Log($"[DAY {currentTurn}] {city.cityName}: {item.unitType} production — {item.turnsRemaining} days left");
                    
                    if (item.turnsRemaining <= 0)
                    {
                        Debug.Log($"[DAY {currentTurn}] {city.cityName}: PRODUCTION COMPLETE — calling CompleteDailyProduction for {item.unitType}");
                        city.CompleteDailyProduction(item);
                        city.productionQueue.RemoveAt(0);
                        Debug.Log($"[DAY {currentTurn}] {city.cityName}: Queue now has {city.productionQueue.Count} items. Total armies: {armies.Count}");
                    }
                }
            }
            
            // === Daily building construction for ALL provinces ===
            foreach (var prov in provinces.Values)
            {
                foreach (var slot in prov.buildings)
                {
                    if (slot.isConstructing && slot.turnsToComplete > 0)
                    {
                        slot.turnsToComplete--;
                        if (slot.turnsToComplete <= 0)
                        {
                            slot.isConstructing = false;
                            Debug.Log($"[CampaignManager] Building {slot.type} completed in {prov.provinceName}!");
                        }
                    }
                }
            }
            
            // === Daily building construction for CityData buildings ===
            foreach (var city in cities.Values)
            {
                foreach (var bld in city.buildings)
                {
                    if (bld.isConstructing && bld.constructionTurnsRemaining > 0)
                    {
                        bld.constructionTurnsRemaining--;
                        if (bld.constructionTurnsRemaining <= 0)
                        {
                            bld.isConstructed = true;
                            bld.isConstructing = false;
                            Debug.Log($"[CampaignManager] Building {bld.buildingName} completed in {city.cityName}!");
                        }
                    }
                }
            }
            
            // === Daily research progress for ALL factions ===
            foreach (var kvp in factions)
            {
                if (kvp.Value.isEliminated) continue;
                FactionData fd = kvp.Value;
                if (fd.techTree == null || fd.techTree.CurrentResearchId == null) continue;
                
                float speed = TechTree.CalculateResearchSpeedMultiplier(fd, provinces);
                Technology completed = fd.techTree.AdvanceDailyResearch(speed);
                
                if (completed != null)
                {
                    AddFactionEvent(kvp.Key, $"Recherche terminée : {completed.name}!");
                    Debug.Log($"[Day {currentTurn}] {kvp.Key} completed research: {completed.name} (speed was {speed:F1}x)");
                }
            }
            
            // === Daily army RATIONS consumption ===
            bool isWinter = DifficultyManager.IsWinterTurn(currentTurn);
            foreach (var army in armies.Values)
            {
                // Determine territory status
                bool inFriendly = true;
                if (provinces.TryGetValue(army.currentProvinceId, out var armyProv2))
                    inFriendly = armyProv2.owner == army.faction;

                army.ConsumeDailyRations(inFriendly, isWinter);

                if (army.isStarving)
                {
                    // Halt movement — army cannot advance without food
                    if (army.isMoving)
                    {
                        army.marchDaysRemaining = 0;
                        army.isMoving = false;
                        AddFactionEvent(army.faction, $"⚠ {army.armyName} est à court de vivres et ne peut plus avancer !");
                        Debug.Log($"[Day {currentTurn}] {army.armyName} STARVING — movement halted!");
                    }

                    // Apply starvation attrition + fatigue
                    army.ApplyStarvationEffects();
                }
                else if (army.isLowOnFood)
                {
                    // Low on food — org drops
                    army.organization = Mathf.Max(0f, army.organization - 5f);
                }
            }

            // === Daily army march processing ===
            var map3D = FindAnyObjectByType<CampaignMap3D>();
            foreach (var army in armies.Values)
            {
                if (!army.isMoving || army.marchDaysRemaining <= 0) continue;
                
                army.marchDaysRemaining--;
                army.turnsCampaigning++;
                army.fatigue = Mathf.Min(1f, army.fatigue + 0.05f);
                
                if (army.marchDaysRemaining <= 0)
                {
                    // Don't finalize here — let UpdateArmyPositions handle the visual arrival
                    // and province assignment (it has access to world positions)
                    // Just mark as no more days remaining
                    army.marchDaysRemaining = 0;
                    
                    Debug.Log($"[Campaign] {army.armyName} arrived at {army.currentProvinceId}!");
                    
                    // Check for battle at destination
                    if (provinces.TryGetValue(army.currentProvinceId, out var arrivalProv))
                    {
                        if (arrivalProv.owner != army.faction && AreAtWar(army.faction, arrivalProv.owner))
                        {
                            ArmyData enemy = FindArmyInProvince(army.currentProvinceId, army.faction);
                            if (enemy == null) enemy = new ArmyData("Garrison", "garrison_" + army.currentProvinceId, arrivalProv.owner, army.currentProvinceId);
                            OnBattleTriggered?.Invoke(army, enemy, arrivalProv);
                        }
                    }
                }
            }
            
            // === Daily trespass penalty: armies in neutral territory reduce relations ===
            foreach (var army in armies.Values)
            {
                if (army.isMoving) continue; // Only stationary armies cause trespass
                if (!provinces.TryGetValue(army.currentProvinceId, out var armyProv)) continue;
                if (armyProv.owner == army.faction || !factions.ContainsKey(armyProv.owner)) continue;
                if (AreAtWar(army.faction, armyProv.owner)) continue; // Already at war, no trespass
                
                // -2 per day while trespassing
                ApplyDiplomaticPenalty(army.faction, armyProv.owner, -2, 
                    $"Occupation militaire de {armyProv.provinceName}");
            }
            
            // Fire turn-changed event for UI refresh (date display etc.)
            OnTurnChanged?.Invoke(currentTurn, playerFaction);
        }

        /// <summary>
        /// Heavy seasonal processing — called every 90 game days (= 1 season = ~7.5 min at 1×).
        /// Handles: economy, buildings, tech, AI decisions, supply, diplomacy, events.
        /// </summary>
        public void ProcessSeasonTick()
        {
            // Process building construction and research
            BuildingManager.Instance?.ProcessTurn();

            // Process ALL factions (player + AI) simultaneously
            foreach (var kvp in factions)
            {
                if (kvp.Value.isEliminated) continue;
                ProcessFactionTurn(kvp.Key);
            }

            // Supply system — apply attrition
            SupplySystem.ProcessSupplyTurn(armies, factions);

            // Supply line system — calculate supply chains, draw from depots, apply advanced attrition
            SupplyLineSystem.ProcessSupplyTurn(this);

            // Co-op request expiry
            Network.CoopRequestSystem.Instance?.ProcessTurnExpiry(currentTurn);

            // Faction turn assignment (legacy)
            currentFactionTurn = playerFaction;

            // Reset movement for all armies
            foreach (var army in armies.Values)
                army.ResetMovement();
            
            // Process agent turns
            AgentManager.Instance?.ProcessEndOfTurn();
            
            // Process research assignment turns
            ResearchAssignmentManager.Instance?.ProcessEndOfTurn();

            // Naval system
            NavalSystem.ProcessNavalTurn(this);

            // Supply depots — cleanup lost provinces
            SupplyLineSystem.CleanupDepots(this);

            // Diplomacy
            WarJustificationSystem.ProcessTurn(currentTurn);
            
            // Logistics convoys & supply chains
            LogisticsConvoySystem.ProcessTurn(provinces);

            // Victory check
            var victory = WarJustificationSystem.CheckVictoryProgress(playerFaction, currentTurn);
            if (victory != null)
                AddFactionEvent(playerFaction, $"VICTOIRE: {WarJustificationSystem.GetVictoryName(victory.type)}!");

            // Historical events
            if (eventSystem != null)
                eventSystem.CheckEvents(this, currentTurn);

            // Autosave every year (4 seasons)
            if (currentTurn % 4 == 0)
                AutoSave();

            // Refresh city and convoy visuals
            var map3D = FindAnyObjectByType<CampaignMap3D>();
            if (map3D != null)
            {
                map3D.RefreshCityVisuals();
                map3D.RefreshConvoyVisuals();
            }

            OnTurnChanged?.Invoke(currentTurn, playerFaction);
            Debug.Log($"[RealTime] Season tick #{currentTurn} — {SeasonSystem.DateString}");
        }

        private void ProcessFactionTurn(FactionType faction)
        {
            if (!factions.ContainsKey(faction)) return;
            FactionData fd = factions[faction];

            // Advance tech research
            if (fd.techTree != null)
            {
                Technology completed = fd.techTree.AdvanceResearch();
                if (completed != null)
                {
                    Debug.Log($"[{faction}] Technology researched: {completed.name}!");
                    AddFactionEvent(faction, $"Technologie recherchée: {completed.name}");
                }
            }

            // Tech bonuses
            float techGoldBonus = fd.techTree != null ? fd.techTree.TotalGoldIncomeBonus : 0f;
            float techFoodBonus = fd.techTree != null ? fd.techTree.TotalFoodBonus : 0f;
            float techIronBonus = fd.techTree != null ? fd.techTree.TotalIronBonus : 0f;
            float techMaintenanceReduction = fd.techTree != null ? fd.techTree.TotalMaintenanceReduction : 0f;

            // Collect income from provinces (modified by difficulty scarcity + season)
            float diffScarcity = DifficultyManager.Instance.ResourceScarcityMultiplier;
            float seasonFoodMod = SeasonSystem.GetFoodProductionModifier();
            float seasonProdMod = SeasonSystem.GetProductionModifier();
            float totalGold = 0f, totalFood = 0f, totalIron = 0f;
            foreach (string pid in fd.ownedProvinceIds)
            {
                if (!provinces.ContainsKey(pid)) continue;
                ProvinceData prov = provinces[pid];

                float goldMod = (1f + techGoldBonus) * diffScarcity;
                float foodMod = (1f + techFoodBonus) * diffScarcity;
                float ironMod = (1f + techIronBonus) * diffScarcity;

                // Building bonuses
                foreach (var slot in prov.buildings)
                {
                    if (slot.type == BuildingType.Farm) foodMod += 0.3f * slot.level;
                    if (slot.type == BuildingType.Mine) ironMod += 0.4f * slot.level;
                    if (slot.type == BuildingType.Market) goldMod += 0.25f * slot.level;
                }

                // Province dynamics: population, unrest, culture, prosperity
                prov.ProcessProvinceTurn(fd);

                // Devastation and unrest reduce effective income
                float stabilityMod = 1f - (prov.devastation * 0.005f) - (prov.unrest * 0.003f);
                stabilityMod = Mathf.Clamp(stabilityMod, 0.2f, 1f);

                // Prosperity boosts income
                float prosperityMod = 0.7f + (prov.prosperity * 0.006f);

                totalGold += prov.goldIncome * goldMod * stabilityMod * prosperityMod * seasonProdMod;
                totalFood += prov.foodProduction * foodMod * stabilityMod * seasonFoodMod;
                totalIron += prov.ironProduction * ironMod * stabilityMod * seasonProdMod;

                // Check for revolts
                int revoltSize = prov.CheckRevolt();
                if (revoltSize > 0)
                {
                    AddFactionEvent(faction, $"RÉVOLTE à {prov.provinceName}! {revoltSize} rebelles!");
                }

                // Process building construction
                foreach (var slot in prov.buildings)
                {
                    if (slot.isConstructing)
                    {
                        slot.turnsToComplete--;
                        if (slot.turnsToComplete <= 0)
                        {
                            slot.isConstructing = false;
                            slot.level++;
                            UpdateProvinceBuildings(prov);
                            Debug.Log($"{BuildingInfo.GetName(slot.type)} completed in {prov.provinceName}!");
                        }
                    }
                }
            }

            // Process cities in owned provinces
            foreach (var city in cities.Values)
            {
                if (city.owner == faction)
                {
                    city.UpdateCity();
                    
                    // Get city production
                    var production = ProductionChainManager.Instance.GetCityDailyProduction(city);
                    totalGold += production[ResourceType.Gold];
                    totalFood += production[ResourceType.Food];
                    totalIron += production[ResourceType.Iron];
                }
            }

            // Pay army maintenance (reduced by tech)
            float maintenance = 0f;
            foreach (string aid in fd.armyIds)
            {
                if (armies.ContainsKey(aid))
                    maintenance += armies[aid].MaintenanceCost;
            }
            maintenance *= (1f - techMaintenanceReduction) * DifficultyManager.Instance.MaintenanceCostMultiplier;

            fd.AddIncome(totalGold - maintenance, totalFood, totalIron);
            
            // Process reinforcements for this faction's regiments
            ProcessFactionReinforcements(faction);

            // === HOI4 SYSTEMS PROCESSING ===
            
            // Manpower recovery, PP generation, stability drift, war support
            fd.ProcessTurnResources();

            // Military production (factories produce equipment)
            ProductionManager.ProcessProductionTurn(fd);

            // Construction progress (civilian workshops build buildings)
            var completedProjects = ProductionManager.ProcessConstructionTurn(faction);
            foreach (string pid in completedProjects)
            {
                Debug.Log($"[{faction}] Construction project completed: {pid}");
                AddFactionEvent(faction, $"Construction terminée: {pid}");
            }

            // Equipment attrition (wear and tear on stockpiles)
            EquipmentSystem.ProcessAttrition(fd, armies);

            // Economy: extended resources, budget, debt, inflation
            EconomyManager.ProduceExtendedResources(faction, provinces, fd);
            EconomyManager.ProcessEconomyTurn(faction, totalGold, maintenance);

            // National laws cooldown processing
            NationalLaws.ProcessTurnCooldowns(faction);

            // Supply line calculation and attrition
            SupplyLineSystem.CalculateSupplyForFaction(faction, this);

            // Army depth mechanics: fatigue, org recovery, winter attrition
            float seasonAttritionMod = SeasonSystem.GetAttritionModifier(ProvinceTerrainType.Plains, false);
            foreach (string armyId in fd.armyIds)
            {
                if (!armies.ContainsKey(armyId)) continue;
                var army = armies[armyId];
                army.ProcessFatigue();
                army.RecoverOrganization();
                army.ApplyWinterAttrition(currentTurn);
            }
        }

        private void UpdateProvinceBuildings(ProvinceData province)
        {
            province.hasBarracks = false;
            province.hasStables = false;
            province.hasArmory = false;

            foreach (var slot in province.buildings)
            {
                if (slot.type == BuildingType.Barracks && slot.level > 0) province.hasBarracks = true;
                if (slot.type == BuildingType.Stables && slot.level > 0) province.hasStables = true;
                if (slot.type == BuildingType.Armory && slot.level > 0) province.hasArmory = true;
            }
        }

        // === ARMY MOVEMENT ===

        /// <summary>
        /// Create a new army in a province or return existing friendly army there.
        /// Used when units are produced in cities.
        /// </summary>
        public ArmyData CreateOrGetArmyInProvince(string provinceId, FactionType faction)
        {
            // Check if we already have an army in this province
            foreach (var army in armies.Values)
            {
                if (army.currentProvinceId == provinceId && army.faction == faction)
                    return army;
            }

            // Create a new army
            string cityName = GetProvinceName(provinceId);
            string armyId = $"army_{faction}_{provinceId}_{System.Guid.NewGuid().ToString().Substring(0, 6)}";
            string armyName = $"Armée de {cityName}";

            ArmyData newArmy = new ArmyData(armyName, armyId, faction, provinceId);
            armies[armyId] = newArmy;

            if (factions.ContainsKey(faction))
                factions[faction].armyIds.Add(armyId);

            Debug.Log($"[CampaignManager] Created army '{armyName}' in {cityName} for {faction}");
            
            // Immediately create visual marker on the map
            var map3D = FindAnyObjectByType<CampaignMap3D>();
            if (map3D != null)
            {
                try
                {
                    map3D.CreateArmyMarker(newArmy);
                    Debug.Log($"[CampaignManager] Visual marker created for '{armyName}'");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CampaignManager] Failed to create marker for '{armyName}': {e}");
                }
            }
            else
            {
                Debug.LogWarning($"[CampaignManager] No CampaignMap3D found — cannot create visual marker!");
            }
            
            return newArmy;
        }

        public bool MoveArmy(string armyId, string targetProvinceId)
        {
            if (!armies.TryGetValue(armyId, out var army))
            {
                Debug.LogWarning($"[MoveArmy] Army '{armyId}' NOT FOUND in armies dict (count={armies.Count})");
                return false;
            }
            
            // MULTIPLAYER CHECK: Only if actually connected to a multiplayer session
            // Skip this entirely in single-player to avoid accidentally queuing actions
            if (NapoleonicWars.Network.NetworkLobbyManager.Instance != null && 
                NapoleonicWars.Network.NetworkLobbyManager.Instance.IsConnected &&
                Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsClient)
            {
                var netCampaign = NapoleonicWars.Network.NetworkCampaignManager.Instance;
                if (netCampaign != null && !netCampaign.IsServerProcessingActions)
                {
                    netCampaign.QueueActionServerRpc(new NapoleonicWars.Network.NetworkCampaignAction
                    {
                        faction = army.faction,
                        actionType = NapoleonicWars.Network.CampaignActionType.MoveArmy,
                        sourceId = armyId,
                        targetId = targetProvinceId,
                        priority = 10
                    });
                    Debug.Log($"[MoveArmy] Army {armyId} movement queued for network turn resolution.");
                    return true;
                }
            }

            // Resolve current position — if army is already marching, use its current province
            string currentProvId = army.isMoving ? army.originProvinceId : army.currentProvinceId;
            if (string.IsNullOrEmpty(currentProvId)) currentProvId = army.currentProvinceId;
            
            if (!provinces.TryGetValue(currentProvId, out var currentProv))
            {
                Debug.LogWarning($"[MoveArmy] Current province '{currentProvId}' NOT FOUND");
                return false;
            }
            if (!provinces.TryGetValue(targetProvinceId, out var targetProv))
            {
                Debug.LogWarning($"[MoveArmy] Target province '{targetProvinceId}' NOT FOUND");
                return false;
            }
            
            // Same province check
            if (currentProvId == targetProvinceId)
            {
                Debug.LogWarning($"[MoveArmy] ❌ {army.armyName} already at target '{targetProv.provinceName}' (currentProvId='{currentProvId}' == targetId='{targetProvinceId}')");
                return false;
            }
            
            // === DIPLOMATIC CONSEQUENCES for entering foreign territory ===
            if (targetProv.owner != army.faction && factions.ContainsKey(targetProv.owner))
            {
                if (AreAtWar(army.faction, targetProv.owner))
                {
                    Debug.Log($"[Campaign] {army.armyName} marching into enemy territory: {targetProv.provinceName}");
                }
                else
                {
                    Debug.Log($"[Campaign] ⚠ {army.armyName} trespassing into {targetProv.owner}'s territory ({targetProv.provinceName})!");
                    ApplyDiplomaticPenalty(army.faction, targetProv.owner, -10, "Trespass militaire");
                }
            }

            // Start multi-day march to target province
            army.originProvinceId = army.currentProvinceId;
            army.targetProvinceId = targetProvinceId;
            army.isMoving = true;
            army.isResting = false;
            
            // Calculate march days: base distance + terrain modifier
            // Map positions are normalized 0-1, so distance ~0.05-0.3 between provinces
            float distance = Vector2.Distance(currentProv.mapPosition, targetProv.mapPosition);
            int distanceDays = Mathf.Max(1, Mathf.RoundToInt(distance * 30f));
            
            float terrainMult = 1f;
            switch (targetProv.terrainType)
            {
                case ProvinceTerrainType.Mountains: terrainMult = 2.0f; break;
                case ProvinceTerrainType.Forest:
                case ProvinceTerrainType.Hills:
                case ProvinceTerrainType.Marsh: terrainMult = 1.5f; break;
                case ProvinceTerrainType.Plains:
                case ProvinceTerrainType.Desert:
                case ProvinceTerrainType.Coastal: terrainMult = 1.0f; break;
                case ProvinceTerrainType.Urban: terrainMult = 1.2f; break;
                default: terrainMult = 1.0f; break;
            }
            
            int totalDays = Mathf.Max(1, Mathf.RoundToInt(distanceDays * terrainMult));
            army.marchDaysRemaining = totalDays;
            army.marchDaysTotal = totalDays;
            
            // Give immediate visual progress so the army starts moving THIS frame
            // instead of waiting for the next day tick (5 seconds)
            army.marchDaysRemaining = Mathf.Max(0, totalDays - 1);
            
            Debug.Log($"[MoveArmy] ✅ {army.armyName} begins march to {targetProv.provinceName} ({totalDays} days, dist={distance:F3}, terrain={targetProv.terrainType})");

            return true;
        }

        /// <summary>
        /// Move army to any world position (free movement — not limited to province centers).
        /// </summary>
        public bool MoveArmyToPosition(string armyId, Vector3 worldPos, Vector3 currentWorldPos)
        {
            if (!armies.TryGetValue(armyId, out var army))
            {
                Debug.LogWarning($"[MoveArmyFree] Army '{armyId}' NOT FOUND");
                return false;
            }

            // Only player can order moves
            if (army.faction != playerFaction)
            {
                Debug.LogWarning($"[MoveArmyFree] {army.armyName} is {army.faction}, not player");
                return false;
            }

            // Record origin position (current marker position in world space)
            army.originWorldPosition = currentWorldPos;
            army.targetWorldPosition = worldPos;
            army.isMoving = true;
            army.isResting = false;

            // Remember origin province for game logic
            army.originProvinceId = army.currentProvinceId;

            // targetProvinceId will be updated on arrival to nearest province
            army.targetProvinceId = army.currentProvinceId;

            // Calculate march days from 3D distance
            float dist3D = Vector3.Distance(currentWorldPos, worldPos);
            int totalDays = Mathf.Max(1, Mathf.RoundToInt(dist3D / 200f)); // ~200 units per day

            army.marchDaysRemaining = totalDays;
            army.marchDaysTotal = totalDays;

            Debug.Log($"[MoveArmyFree] ✅ {army.armyName} moving to world({worldPos.x:F0},{worldPos.y:F0},{worldPos.z:F0}), dist={dist3D:F0}, {totalDays} days");
            return true;
        }

        private ArmyData FindArmyInProvince(string provinceId, FactionType excludeFaction)
        {
            foreach (var army in armies.Values)
            {
                if (army.currentProvinceId == provinceId && army.faction != excludeFaction)
                    return army;
            }
            return null;
        }

        // === DIPLOMACY: TRESPASS SYSTEM ===

        /// <summary>
        /// Apply diplomatic penalty. At -50 opinion, victim declares war (hybrid war).
        /// </summary>
        public void ApplyDiplomaticPenalty(FactionType aggressor, FactionType victim, int amount, string reason)
        {
            if (aggressor == victim) return;
            if (!factions.TryGetValue(victim, out var victimFaction)) return;
            
            if (!victimFaction.relations.ContainsKey(aggressor))
                victimFaction.relations[aggressor] = new DiplomaticRelation();
            
            var rel = victimFaction.relations[aggressor];
            rel.relationScore += amount;
            Debug.Log($"[Diplomacy] {aggressor} → {victim}: {amount} ({reason}). Opinion: {rel.relationScore}");
            
            // Hybrid war threshold: at -50 opinion, declare war
            if (rel.relationScore <= -50 && rel.state != DiplomacyState.War)
            {
                Debug.Log($"[Diplomacy] ⚠ {victim} declares war on {aggressor} due to '{reason}'!");
                DeclareWar(aggressor, victim);
            }
        }

        // === BUILDING ===

        public bool BuildInProvince(string provinceId, int slotIndex, BuildingType type)
        {
            if (!provinces.ContainsKey(provinceId)) return false;
            ProvinceData prov = provinces[provinceId];

            if (prov.owner != playerFaction) return false;
            if (slotIndex < 0 || slotIndex >= prov.buildings.Length) return false;

            BuildingSlot slot = prov.buildings[slotIndex];
            if (slot.isConstructing) return false;

            // Check if building is unlocked by technology
            if (!IsBuildingUnlocked(type))
            {
                Debug.Log($"Cannot build {BuildingInfo.GetName(type)} - technology not researched.");
                return false;
            }

            // Check population requirement for advanced buildings
            int requiredPop = GetBuildingPopulationRequirement(type);
            if (prov.population < requiredPop)
            {
                Debug.Log($"Cannot build {BuildingInfo.GetName(type)} - population {prov.population} < required {requiredPop}");
                return false;
            }

            int cost = BuildingInfo.GetCostGold(type, slot.level);
            FactionData fd = factions[playerFaction];

            if (!fd.CanAfford(cost)) return false;

            fd.Spend(cost);
            slot.type = type;
            slot.isConstructing = true;
            slot.turnsToComplete = BuildingInfo.GetBuildTime(type);

            Debug.Log($"Started building {BuildingInfo.GetName(type)} in {prov.provinceName} ({slot.turnsToComplete} turns)");
            return true;
        }
        
        /// <summary>
        /// Check if a building type is unlocked (by technology or default)
        /// </summary>
        public bool IsBuildingUnlocked(BuildingType type)
        {
            // Basic buildings are always unlocked
            if (type == BuildingType.Farm || type == BuildingType.Mine || 
                type == BuildingType.Market || type == BuildingType.Church ||
                type == BuildingType.Fortress || type == BuildingType.Barracks ||
                type == BuildingType.Stables || type == BuildingType.Armory ||
                type == BuildingType.University)
            {
                return true;
            }
            
            // Check UnitTechnologySystem for unlocked buildings
            if (UnitTechnologySystem.Instance != null)
            {
                return UnitTechnologySystem.Instance.IsBuildingUnlocked(type);
            }
            
            // Check TechTree directly
            var techTree = GetPlayerTechTree();
            if (techTree != null)
            {
                var allTechs = techTree.GetAllTechs();
                foreach (var kvp in allTechs)
                {
                    if (techTree.IsResearched(kvp.Key) && kvp.Value.unlocksBuilding && kvp.Value.unlockedBuilding == type)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Get population requirement for building construction
        /// </summary>
        private int GetBuildingPopulationRequirement(BuildingType type)
        {
            return type switch
            {
                BuildingType.SmallArtillerySchool => 0,
                BuildingType.ProvincialArtillerySchool => 5000,
                BuildingType.RoyalArtilleryAcademy => 15000,
                BuildingType.GrandArtilleryAcademy => 25000,
                BuildingType.ImperialArtilleryAcademy => 30000,
                BuildingType.VillageBarracks => 0,
                BuildingType.ProvincialBarracks => 8000,
                BuildingType.MilitaryAcademy => 12000,
                BuildingType.RoyalMilitaryCollege => 20000,
                BuildingType.MilitaryUniversity => 40000,
                BuildingType.University => 5000,
                _ => 0
            };
        }

        /// <summary>
        /// Check if province has a specific building type
        /// </summary>
        private bool HasBuildingType(ProvinceData prov, BuildingType type)
        {
            foreach (var slot in prov.buildings)
            {
                if (slot.type == type && !slot.isConstructing && slot.level > 0)
                    return true;
            }
            return false;
        }

        public bool ResearchTechnology(string techId)
        {
            FactionData fd = GetPlayerFaction();
            if (fd == null || fd.techTree == null) return false;
            return fd.techTree.StartResearch(techId, fd);
        }

        // === AGENT SYSTEM ===
        
        /// <summary>
        /// Recruit a new intelligence agent
        /// </summary>
        public bool RecruitAgent(AgentType type, string provinceId)
        {
            ProvinceData prov = provinces.ContainsKey(provinceId) ? provinces[provinceId] : null;
            if (prov == null) return false;
            if (prov.owner != playerFaction) return false;
            
            // For now, allow recruiting anywhere - later will require spy building
            // TODO: Add building requirement check when spy building exists
            
            AgentData agent = AgentManager.Instance?.CreateAgent(type, playerFaction, provinceId);
            return agent != null;
        }
        
        /// <summary>
        /// Move an agent to a target province
        /// </summary>
        public bool MoveAgent(string agentId, string targetProvinceId)
        {
            if (AgentManager.Instance == null) return false;
            
            AgentData agent = null;
            foreach (var a in AgentManager.Instance.GetAgentsForFaction(playerFaction))
            {
                if (a.agentId == agentId)
                {
                    agent = a;
                    break;
                }
            }
            
            if (agent == null) return false;
            return AgentManager.Instance.MoveAgent(agent, targetProvinceId);
        }

        public TechTree GetPlayerTechTree()
        {
            FactionData fd = GetPlayerFaction();
            return fd?.techTree;
        }

        // === UI DATA HELPERS ===

        public struct IncomeBreakdown
        {
            public float goldIncome;
            public float goldMaintenance;
            public float goldNet;
            public float foodIncome;
            public float ironIncome;
            public float cityGold;
            public float cityFood;
            public float cityIron;
            public float tradeGold;
        }

        /// <summary>Calculate per-turn income breakdown for UI display without mutating state.</summary>
        public IncomeBreakdown CalculateIncomeBreakdown(FactionType faction)
        {
            var result = new IncomeBreakdown();
            if (!factions.ContainsKey(faction)) return result;
            FactionData fd = factions[faction];

            float techGoldBonus = fd.techTree != null ? fd.techTree.TotalGoldIncomeBonus : 0f;
            float techFoodBonus = fd.techTree != null ? fd.techTree.TotalFoodBonus : 0f;
            float techIronBonus = fd.techTree != null ? fd.techTree.TotalIronBonus : 0f;
            float techMaintenanceReduction = fd.techTree != null ? fd.techTree.TotalMaintenanceReduction : 0f;
            float diffScarcity = DifficultyManager.Instance.ResourceScarcityMultiplier;

            foreach (string pid in fd.ownedProvinceIds)
            {
                if (!provinces.ContainsKey(pid)) continue;
                ProvinceData prov = provinces[pid];

                float goldMod = (1f + techGoldBonus) * diffScarcity;
                float foodMod = (1f + techFoodBonus) * diffScarcity;
                float ironMod = (1f + techIronBonus) * diffScarcity;

                foreach (var slot in prov.buildings)
                {
                    if (slot.type == BuildingType.Farm) foodMod += 0.3f * slot.level;
                    if (slot.type == BuildingType.Mine) ironMod += 0.4f * slot.level;
                    if (slot.type == BuildingType.Market) goldMod += 0.25f * slot.level;
                }

                result.goldIncome += prov.goldIncome * goldMod;
                result.foodIncome += prov.foodProduction * foodMod;
                result.ironIncome += prov.ironProduction * ironMod;
            }

            // City production
            foreach (var city in cities.Values)
            {
                if (city.owner == faction && ProductionChainManager.Instance != null)
                {
                    var production = ProductionChainManager.Instance.GetCityDailyProduction(city);
                    result.cityGold += production[ResourceType.Gold];
                    result.cityFood += production[ResourceType.Food];
                    result.cityIron += production[ResourceType.Iron];
                }
            }
            result.goldIncome += result.cityGold;
            result.foodIncome += result.cityFood;
            result.ironIncome += result.cityIron;

            // Trade income
            if (TradeSystem.Instance != null)
            {
                foreach (var route in TradeSystem.Instance.TradeRoutes.Values)
                {
                    if (route.isActive && route.fromFaction == faction)
                        result.tradeGold += route.goldPerTurn;
                    if (route.isActive && route.toFaction == faction)
                        result.tradeGold += route.goldPerTurn * 0.5f;
                }
                foreach (var agreement in TradeSystem.Instance.TradeAgreements.Values)
                {
                    if (!agreement.isActive) continue;
                    if (agreement.faction1 == faction) result.tradeGold += agreement.goldPerTurnFaction1;
                    if (agreement.faction2 == faction) result.tradeGold += agreement.goldPerTurnFaction2;
                }
            }
            result.goldIncome += result.tradeGold;

            // Maintenance
            foreach (string aid in fd.armyIds)
            {
                if (armies.ContainsKey(aid))
                    result.goldMaintenance += armies[aid].MaintenanceCost;
            }
            result.goldMaintenance *= (1f - techMaintenanceReduction) * DifficultyManager.Instance.MaintenanceCostMultiplier;

            result.goldNet = result.goldIncome - result.goldMaintenance;
            return result;
        }

        /// <summary>Get province display name from ID.</summary>
        public string GetProvinceName(string provinceId)
        {
            if (string.IsNullOrEmpty(provinceId)) return "—";
            if (provinces.TryGetValue(provinceId, out var prov))
                return prov.provinceName;
            return provinceId;
        }

        /// <summary>Get faction subtitle for UI (e.g. "Empire Français").</summary>
        public static string GetFactionSubtitle(FactionType type)
        {
            return type switch
            {
                FactionType.France => "Empire Français",
                FactionType.Britain => "Royaume-Uni",
                FactionType.Prussia => "Royaume de Prusse",
                FactionType.Russia => "Empire Russe",
                FactionType.Austria => "Empire d'Autriche",
                FactionType.Spain => "Royaume d'Espagne",
                FactionType.Ottoman => "Empire Ottoman",
                _ => type.ToString()
            };
        }

        // === FACTION EVENT LOG ===
        private Dictionary<FactionType, List<string>> factionEvents = new Dictionary<FactionType, List<string>>();
        private const int MaxEvents = 15;

        public void AddFactionEvent(FactionType faction, string message)
        {
            if (!factionEvents.ContainsKey(faction))
                factionEvents[faction] = new List<string>();
            factionEvents[faction].Insert(0, message);
            if (factionEvents[faction].Count > MaxEvents)
                factionEvents[faction].RemoveAt(MaxEvents);
        }

        public List<string> GetFactionEvents(FactionType faction)
        {
            if (factionEvents.TryGetValue(faction, out var events))
                return events;
            return new List<string>();
        }

        public string GetLatestFactionEvent(FactionType faction)
        {
            if (factionEvents.TryGetValue(faction, out var events) && events.Count > 0)
                return events[0];
            return null;
        }

        // === RECRUITMENT ===

        public bool RecruitRegiment(string armyId, UnitType unitType)
        {
            if (!armies.ContainsKey(armyId)) return false;
            ArmyData army = armies[armyId];

            if (army.faction != playerFaction) return false;
            if (!provinces.ContainsKey(army.currentProvinceId)) return false;

            ProvinceData prov = provinces[army.currentProvinceId];
            if (prov.owner != playerFaction) return false;

            FactionData fd = factions[playerFaction];

            // Check tech tree unit unlock
            if (fd.techTree != null && !fd.techTree.IsUnitTypeUnlocked(unitType))
            {
                Debug.Log($"Cannot recruit {unitType} — technology not researched.");
                return false;
            }

            // Check building requirements - tiered system per unit branch
            bool canRecruit = false;
            switch (unitType)
            {
                // === INFANTRY BRANCH ===
                case UnitType.Militia:
                case UnitType.TrainedMilitia:
                    canRecruit = prov.hasBarracks || HasBuildingType(prov, BuildingType.VillageBarracks) || 
                                 HasBuildingType(prov, BuildingType.ProvincialBarracks);
                    break;
                case UnitType.LineInfantry:
                case UnitType.LightInfantry:
                case UnitType.Fusilier:
                    canRecruit = HasBuildingType(prov, BuildingType.VillageBarracks) || 
                                 HasBuildingType(prov, BuildingType.ProvincialBarracks);
                    break;
                case UnitType.Grenadier:
                case UnitType.Voltigeur:
                    canRecruit = HasBuildingType(prov, BuildingType.ProvincialBarracks) || 
                                 HasBuildingType(prov, BuildingType.MilitaryAcademy);
                    break;
                case UnitType.Chasseur:
                    canRecruit = HasBuildingType(prov, BuildingType.MilitaryAcademy) ||
                                 HasBuildingType(prov, BuildingType.RoyalMilitaryCollege);
                    break;
                case UnitType.GuardInfantry:
                    canRecruit = HasBuildingType(prov, BuildingType.RoyalMilitaryCollege) || 
                                 HasBuildingType(prov, BuildingType.MilitaryUniversity);
                    break;
                case UnitType.OldGuard:
                    canRecruit = HasBuildingType(prov, BuildingType.MilitaryUniversity);
                    break;

                // === CAVALRY BRANCH ===
                case UnitType.MilitiaCavalry:
                case UnitType.Dragoon:
                    canRecruit = prov.hasStables;
                    break;
                case UnitType.Cavalry:
                    canRecruit = prov.hasStables && (prov.hasBarracks || HasBuildingType(prov, BuildingType.VillageBarracks));
                    break;
                case UnitType.Hussar:
                case UnitType.Lancer:
                    canRecruit = HasBuildingType(prov, BuildingType.ProvincialBarracks) && prov.hasStables;
                    break;
                case UnitType.Cuirassier:
                    canRecruit = HasBuildingType(prov, BuildingType.MilitaryAcademy) && prov.hasStables;
                    break;
                case UnitType.GuardCavalry:
                    canRecruit = HasBuildingType(prov, BuildingType.RoyalMilitaryCollege);
                    break;
                case UnitType.Mameluke:
                    canRecruit = HasBuildingType(prov, BuildingType.MilitaryUniversity);
                    break;

                // === ARTILLERY BRANCH ===
                case UnitType.GarrisonCannon:
                    canRecruit = prov.hasArmory || HasBuildingType(prov, BuildingType.SmallArtillerySchool);
                    break;
                case UnitType.Artillery:
                    canRecruit = HasBuildingType(prov, BuildingType.SmallArtillerySchool) ||
                                 HasBuildingType(prov, BuildingType.ProvincialArtillerySchool);
                    break;
                case UnitType.HorseArtillery:
                    canRecruit = HasBuildingType(prov, BuildingType.ProvincialArtillerySchool) && prov.hasStables;
                    break;
                case UnitType.Howitzer:
                    canRecruit = HasBuildingType(prov, BuildingType.RoyalArtilleryAcademy);
                    break;
                case UnitType.GrandBattery:
                    canRecruit = HasBuildingType(prov, BuildingType.GrandArtilleryAcademy);
                    break;
                case UnitType.GuardArtillery:
                    canRecruit = HasBuildingType(prov, BuildingType.ImperialArtilleryAcademy);
                    break;

                // === SPECIAL UNITS ===
                case UnitType.Engineer:
                    canRecruit = HasBuildingType(prov, BuildingType.MilitaryAcademy);
                    break;
                case UnitType.Sapper:
                    canRecruit = HasBuildingType(prov, BuildingType.RoyalMilitaryCollege);
                    break;
                case UnitType.Marine:
                    canRecruit = HasBuildingType(prov, BuildingType.ProvincialBarracks) && prov.isCoastal;
                    break;
                case UnitType.Partisan:
                    canRecruit = prov.hasBarracks || HasBuildingType(prov, BuildingType.VillageBarracks);
                    break;
            }

            if (!canRecruit) 
            {
                Debug.Log($"Cannot recruit {unitType} - missing required building in {prov.provinceName}");
                return false;
            }

            // Apply tech recruit cost reduction
            float costReduction = fd.techTree != null ? fd.techTree.TotalRecruitCostReduction : 0f;
            int baseCost = RegimentData.GetRecruitCostGold(unitType);
            int cost = Mathf.RoundToInt(baseCost * (1f - costReduction));

            if (!fd.CanAfford(cost)) return false;

            fd.Spend(cost);
            int size = RegimentData.GetDefaultSize(unitType);
            string name = $"New {unitType} Regiment";
            army.AddRegiment(new RegimentData(name, unitType, size));

            Debug.Log($"Recruited {name} in {prov.provinceName} (cost: {cost}g)");
            return true;
        }
        
        /// <summary>
        /// Reinforce a damaged regiment back to full strength.
        /// Must be in a friendly city with required buildings.
        /// Takes time based on how many troops need to be replaced.
        /// </summary>
        public bool ReinforceRegiment(string armyId, int regimentIndex)
        {
            if (!armies.ContainsKey(armyId)) return false;
            ArmyData army = armies[armyId];
            
            // Validate regiment index
            if (regimentIndex < 0 || regimentIndex >= army.regiments.Count) return false;
            RegimentData regiment = army.regiments[regimentIndex];
            
            // Check if regiment needs reinforcement
            if (!regiment.needsReinforcement) return false;
            
            // Check if army is in a friendly province
            if (!provinces.ContainsKey(army.currentProvinceId)) return false;
            ProvinceData prov = provinces[army.currentProvinceId];
            if (prov.owner != army.faction) return false;
            
            // Check if army is in a city (not in the field)
            CityData city = GetCityForProvince(army.currentProvinceId);
            if (city == null) return false;
            
            // Check building requirements based on unit type
            bool canReinforce = false;
            switch (regiment.unitType)
            {
                case UnitType.LineInfantry:
                case UnitType.LightInfantry:
                case UnitType.Grenadier:
                    canReinforce = prov.hasBarracks;
                    break;
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    canReinforce = prov.hasStables;
                    break;
                case UnitType.Artillery:
                    canReinforce = prov.hasArmory;
                    break;
            }
            
            if (!canReinforce)
            {
                Debug.Log($"Cannot reinforce {regiment.regimentName} - missing required building in {prov.provinceName}");
                return false;
            }
            
            // Calculate reinforcement cost (50% of new recruitment cost per missing soldier)
            int missingTroops = regiment.maxSize - regiment.currentSize;
            int baseCostPerTroop = RegimentData.GetRecruitCostGold(regiment.unitType) / RegimentData.GetDefaultSize(regiment.unitType);
            int totalCost = missingTroops * Mathf.RoundToInt(baseCostPerTroop * 0.5f);
            
            FactionData fd = factions[army.faction];
            if (!fd.CanAfford(totalCost))
            {
                Debug.Log($"Cannot afford reinforcement for {regiment.regimentName} - costs {totalCost}g");
                return false;
            }
            
            // Pay for reinforcement
            fd.Spend(totalCost);
            
            // Set up reinforcement data
            regiment.isBeingReinforced = true;
            regiment.targetReinforcementSize = regiment.maxSize;
            regiment.reinforcementCostGold = totalCost;
            
            // Calculate turns needed (base 1 turn + 1 turn per 20 missing troops)
            regiment.turnsToCompleteReinforcement = 1 + Mathf.CeilToInt(missingTroops / 20f);
            
            Debug.Log($"Started reinforcing {regiment.regimentName} in {city.cityName}: {missingTroops} troops, {totalCost}g, {regiment.turnsToCompleteReinforcement} turns");
            return true;
        }
        
        /// <summary>
        /// Process reinforcements for a specific faction's regiments
        /// </summary>
        private void ProcessFactionReinforcements(FactionType faction)
        {
            if (!factions.ContainsKey(faction)) return;
            FactionData fd = factions[faction];
            
            foreach (string aid in fd.armyIds)
            {
                if (!armies.ContainsKey(aid)) continue;
                ArmyData army = armies[aid];
                
                foreach (var regiment in army.regiments)
                {
                    if (!regiment.isBeingReinforced) continue;
                    
                    regiment.turnsToCompleteReinforcement--;
                    
                    if (regiment.turnsToCompleteReinforcement <= 0)
                    {
                        // Reinforcement complete
                        int oldSize = regiment.currentSize;
                        regiment.currentSize = regiment.targetReinforcementSize;
                        regiment.isBeingReinforced = false;
                        regiment.turnsToCompleteReinforcement = 0;
                        regiment.targetReinforcementSize = 0;
                        regiment.reinforcementCostGold = 0;
                        
                        if (faction == playerFaction)
                        {
                            Debug.Log($"{regiment.regimentName} reinforced from {oldSize} to {regiment.currentSize} soldiers");
                        }
                    }
                }
            }
        }

        // === DIPLOMACY ===

        public bool DeclarWar(FactionType target)
        {
            if (!factions.ContainsKey(target)) return false;
            SetDiplomacy(playerFaction, target, DiplomacyState.War);
            Debug.Log($"{playerFaction} declared war on {target}!");
            return true;
        }

        public bool ProposePeace(FactionType target)
        {
            if (!factions.ContainsKey(target)) return false;
            var rel = factions[playerFaction].relations[target];
            if (rel.state != DiplomacyState.War) return false;

            // AI acceptance based on war duration and losses
            if (rel.turnsAtWar > 5)
            {
                SetDiplomacy(playerFaction, target, DiplomacyState.Neutral);
                Debug.Log($"Peace treaty signed with {target}!");
                return true;
            }

            Debug.Log($"{target} rejected peace proposal.");
            return false;
        }

        public bool ProposeAlliance(FactionType target)
        {
            if (!factions.ContainsKey(target)) return false;
            var rel = factions[playerFaction].relations[target];
            if (rel.state == DiplomacyState.War || rel.state == DiplomacyState.Hostile) return false;

            if (rel.relationScore > 50)
            {
                SetDiplomacy(playerFaction, target, DiplomacyState.Alliance);
                Debug.Log($"Alliance formed with {target}!");
                return true;
            }

            Debug.Log($"{target} rejected alliance proposal.");
            return false;
        }

        // === QUERIES ===

        public FactionData GetPlayerFaction()
        {
            return factions.ContainsKey(playerFaction) ? factions[playerFaction] : null;
        }
        
        public FactionData GetFaction(FactionType type)
        {
            return factions.ContainsKey(type) ? factions[type] : null;
        }

        public List<ArmyData> GetArmiesInProvince(string provinceId)
        {
            List<ArmyData> result = new List<ArmyData>();
            foreach (var army in armies.Values)
            {
                if (army.currentProvinceId == provinceId)
                    result.Add(army);
            }
            return result;
        }

        public List<ArmyData> GetPlayerArmies()
        {
            List<ArmyData> result = new List<ArmyData>();
            FactionData fd = GetPlayerFaction();
            if (fd == null) return result;

            foreach (string aid in fd.armyIds)
            {
                if (armies.ContainsKey(aid))
                    result.Add(armies[aid]);
            }
            return result;
        }

        public bool AreAtWar(FactionType a, FactionType b)
        {
            if (!factions.ContainsKey(a) || !factions.ContainsKey(b)) return false;
            if (!factions[a].relations.ContainsKey(b)) return false;
            return factions[a].relations[b].state == DiplomacyState.War;
        }
        
        /// <summary>Declare war between two factions (sets both sides)</summary>
        public void DeclareWar(FactionType a, FactionType b)
        {
            if (a == b) return;
            if (!factions.ContainsKey(a) || !factions.ContainsKey(b)) return;
            
            // Set A's view of B
            if (!factions[a].relations.ContainsKey(b))
                factions[a].relations[b] = new DiplomaticRelation();
            factions[a].relations[b].state = DiplomacyState.War;
            factions[a].relations[b].turnsAtWar = 0;
            
            // Set B's view of A
            if (!factions[b].relations.ContainsKey(a))
                factions[b].relations[a] = new DiplomaticRelation();
            factions[b].relations[a].state = DiplomacyState.War;
            factions[b].relations[a].turnsAtWar = 0;
            
            Debug.Log($"[Diplomacy] WAR DECLARED between {a} and {b}!");
        }

        // === SAVE / LOAD ===

        public bool SaveCampaign(string slotName = "autosave")
        {
            return SaveSystem.Save(this, slotName);
        }

        public bool LoadCampaign(string slotName = "autosave")
        {
            var data = SaveSystem.Load(slotName);
            if (data == null) return false;

            // Clear current state
            provinces.Clear();
            armies.Clear();
            factions.Clear();

            currentTurn = data.currentTurn;
            playerFaction = SaveSystem.ParseFaction(data.playerFaction);

            // Restore factions
            foreach (var fd in data.factions)
            {
                FactionType ft = SaveSystem.ParseFaction(fd.factionType);
                FactionData faction = new FactionData(ft, fd.factionName, ft == playerFaction)
                {
                    gold = fd.gold,
                    food = fd.food,
                    iron = fd.iron,
                    isEliminated = fd.isEliminated,
                    ownedProvinceIds = new List<string>(fd.ownedProvinceIds),
                    armyIds = new List<string>(fd.armyIds)
                };

                // Restore diplomacy
                foreach (var rel in fd.relations)
                {
                    FactionType target = SaveSystem.ParseFaction(rel.targetFaction);
                    faction.relations[target] = new DiplomaticRelation(SaveSystem.ParseDiplomacy(rel.state))
                    {
                        relationScore = rel.relationScore,
                        turnsAtWar = rel.turnsAtWar,
                        turnsOfAlliance = rel.turnsOfAlliance
                    };
                }

                // Restore tech tree
                if (faction.techTree != null)
                {
                    foreach (string techId in fd.researchedTechs)
                    {
                        // Force-complete the tech
                        var allTechs = faction.techTree.GetAllTechs();
                        if (allTechs.ContainsKey(techId))
                        {
                            faction.techTree.StartResearch(techId, faction);
                            for (int i = 0; i < 20; i++) faction.techTree.AdvanceResearch();
                        }
                    }
                    if (!string.IsNullOrEmpty(fd.currentResearchId))
                        faction.techTree.StartResearch(fd.currentResearchId, faction);
                }

                factions[ft] = faction;
            }

            // Restore provinces (need to recreate map first for neighbor data)
            CreateMap();
            foreach (var pd in data.provinces)
            {
                if (!provinces.ContainsKey(pd.id)) continue;
                ProvinceData prov = provinces[pd.id];
                prov.owner = SaveSystem.ParseFaction(pd.owner);
                
                // Restore saved position (from drag editing)
                if (pd.mapPosX != 0f || pd.mapPosY != 0f)
                    prov.mapPosition = new Vector2(pd.mapPosX, pd.mapPosY);

                for (int i = 0; i < pd.buildings.Count && i < prov.buildings.Length; i++)
                {
                    prov.buildings[i].type = SaveSystem.ParseBuildingType(pd.buildings[i].type);
                    prov.buildings[i].level = pd.buildings[i].level;
                    prov.buildings[i].isConstructing = pd.buildings[i].isConstructing;
                    prov.buildings[i].turnsToComplete = pd.buildings[i].turnsToComplete;
                }
            }

            // Restore armies
            foreach (var ad in data.armies)
            {
                FactionType armyFaction = SaveSystem.ParseFaction(ad.faction);
                ArmyData army = new ArmyData(ad.armyName, ad.armyId, armyFaction, ad.currentProvinceId);
                army.movementPoints = ad.movementPoints;

                foreach (var rd in ad.regiments)
                {
                    UnitType ut = SaveSystem.ParseUnitType(rd.unitType);
                    RegimentData reg = new RegimentData(rd.name, ut, rd.maxSize);
                    reg.currentSize = rd.currentSize;
                    reg.experience = rd.experience;
                    army.AddRegiment(reg);
                }

                armies[ad.armyId] = army;
            }

            // Sync city positions with saved province positions
            foreach (var kvp in cities)
            {
                CityData city = kvp.Value;
                if (provinces.ContainsKey(city.provinceId))
                    city.mapPosition = provinces[city.provinceId].mapPosition;
            }

            // Reinitialize events
            eventSystem = new HistoricalEventSystem();

            currentFactionTurn = playerFaction;
            currentPhase = CampaignPhase.PlayerTurn;

            // Refresh 3D map visuals with updated positions
            var map3D = UnityEngine.Object.FindAnyObjectByType<CampaignMap3D>();
            if (map3D != null) map3D.RefreshCityVisuals();

            OnTurnChanged?.Invoke(currentTurn, playerFaction);
            Debug.Log($"[CampaignManager] Campaign loaded: Turn {currentTurn}");
            return true;
        }

        public void AutoSave()
        {
            SaveCampaign("autosave");
        }

        // Transfer methods for diplomacy and siege systems
        public void TransferProvince(string provinceId, FactionType newOwner)
        {
            if (!provinces.TryGetValue(provinceId, out var province)) return;
            
            var oldOwner = province.owner;
            province.owner = newOwner;
            
            // Update faction province lists
            if (factions.ContainsKey(oldOwner))
                factions[oldOwner].ownedProvinceIds.Remove(provinceId);
            if (factions.ContainsKey(newOwner))
                factions[newOwner].ownedProvinceIds.Add(provinceId);
            
            Debug.Log($"[Campaign] Province {province.provinceName} transferred from {oldOwner} to {newOwner}");
        }

        public void TransferCity(string cityId, FactionType newOwner)
        {
            if (!cities.TryGetValue(cityId, out var city)) return;
            
            var oldOwner = city.owner;
            city.owner = newOwner;
            
            Debug.Log($"[Campaign] City {city.cityName} transferred from {oldOwner} to {newOwner}");
        }

        // === BUILDING SYSTEM HELPERS ===

        public CityData GetCity(string cityId)
        {
            return cities.TryGetValue(cityId, out var city) ? city : null;
        }
        
        /// <summary>
        /// Get the CityHierarchy for a province (used for capital-gates-subcity checks)
        /// </summary>
        public CityHierarchy GetCityHierarchy(string provinceId)
        {
            // Look up from CampaignMap3D if available
            var map = FindAnyObjectByType<CampaignMap3D>();
            if (map != null && map.ProvinceHierarchies != null)
            {
                if (map.ProvinceHierarchies.TryGetValue(provinceId, out var hierarchy))
                    return hierarchy;
            }
            return null;
        }

        public bool HasEnoughGold(FactionType faction, int amount)
        {
            if (!factions.TryGetValue(faction, out var fd)) return false;
            return fd.gold >= amount;
        }

        public bool SpendGold(FactionType faction, int amount)
        {
            if (!factions.TryGetValue(faction, out var fd)) return false;
            if (fd.gold < amount) return false;
            fd.gold -= amount;
            return true;
        }

        public void AddGold(FactionType faction, int amount)
        {
            if (factions.TryGetValue(faction, out var fd))
                fd.gold += amount;
        }
    }

    public enum CampaignPhase
    {
        PlayerTurn,
        AITurns,
        Battle,
        BattleResult
    }
}
