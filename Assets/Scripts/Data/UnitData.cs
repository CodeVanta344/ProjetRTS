using UnityEngine;

namespace NapoleonicWars.Data
{
    public enum UnitType
    {
        // ===== INFANTRY BRANCH (10 tiers) =====
        Militia,                // Tier 0 — untrained peasant levy, available from start
        TrainedMilitia,         // Tier 1 — basic drill, slightly better morale
        LineInfantry,           // Tier 2 — standard musket line, backbone of armies
        LightInfantry,          // Tier 3 — skirmishers, faster, lower morale
        Fusilier,               // Tier 4 — disciplined fire, improved accuracy
        Grenadier,              // Tier 5 — elite heavy infantry, high morale
        Voltigeur,              // Tier 6 — French-style elite skirmishers
        Chasseur,               // Tier 7 — elite light infantry, rifles
        GuardInfantry,          // Tier 8 — imperial guard infantry
        OldGuard,               // Tier 9 — Napoleon's immortal veterans, best infantry

        // ===== CAVALRY BRANCH (8 tiers) =====
        MilitiaCavalry,         // Tier 0 — mounted militia, poor training
        Dragoon,                // Tier 1 — mounted infantry, versatile
        Cavalry,                // Tier 2 — standard line cavalry
        Hussar,                 // Tier 3 — light cavalry, fast raiding
        Lancer,                 // Tier 4 — shock lance cavalry
        Cuirassier,             // Tier 5 — armored heavy cavalry
        GuardCavalry,           // Tier 6 — elite mounted guard
        Mameluke,               // Tier 7 — exotic elite cavalry, best horsemen

        // ===== ARTILLERY BRANCH (6 tiers) =====
        GarrisonCannon,         // Tier 0 — basic fixed cannon
        Artillery,              // Tier 1 — standard field artillery
        HorseArtillery,         // Tier 2 — mobile horse-drawn guns
        Howitzer,               // Tier 3 — indirect fire, area damage
        GrandBattery,           // Tier 4 — massed artillery formation
        GuardArtillery,         // Tier 5 — the Emperor's personal guns

        // ===== SPECIAL UNITS (4) =====
        Engineer,               // Builds fortifications, bridges
        Sapper,                 // Siege specialist, breaches walls
        Marine,                 // Naval infantry, coastal assault
        Partisan                // Guerrilla fighters, ambush bonus
    }

    public enum FactionType
    {
        France,
        Britain,
        Prussia,
        Russia,
        Austria,
        Spain,
        Ottoman,
        Portugal,
        Sweden,
        Denmark,
        Poland,
        Venice,
        Dutch,
        Bavaria,
        Saxony,
        PapalStates,
        Savoy,
        Switzerland,
        Genoa,
        Tuscany,
        Hanover,
        Modena,
        Parma,
        Lorraine
    }

    [CreateAssetMenu(fileName = "NewUnitData", menuName = "Napoleonic Wars/Unit Data")]
    public class UnitData : ScriptableObject
    {
        [Header("Identity")]
        public string unitName = "Line Infantry";
        public UnitType unitType = UnitType.LineInfantry;
        public FactionType faction = FactionType.France;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float moveSpeed = 3.5f;
        public float runSpeed = 6f;
        public float attackDamage = 25f;
        public float attackRange = 250f;  // Very long range (trajectory fire possible)
        public float attackCooldown = 3f;
        public float accuracy = 0.75f;     // Base accuracy at optimal range

        [Header("Melee")]
        public float meleeDamage = 15f;
        public float meleeRange = 1.5f;

        [Header("Morale")]
        public float maxMorale = 100f;
        public float moraleLossOnHit = 5f;
        public float moraleLossOnDeath = 2f;
        public float moraleRecoveryRate = 1f;
        public float fleeThreshold = 20f;
        public float moraleLossOnFlanked = 10f;
        public float moraleLossOnCharged = 15f;

        [Header("Charge & Melee Modifiers")]
        public float chargeBonusDamage = 0f;
        public float chargeMinDistance = 10f;
        public float flankingDamageMultiplier = 1.5f;
        public float rearDamageMultiplier = 2f;
        public bool canCharge = false;
        public float chargeSpeedMultiplier = 1.8f;
        public float chargeImpactRadius = 5f;         // Area of effect for charge morale shock
        public float chargeMomentumScaling = 30f;     // Distance for full charge bonus (damage scales with distance charged)

        [Header("Volley Fire")]
        public bool canVolleyFire = true;
        public int volleyRanks = 2;
        public float volleyCooldown = 4f;
        public float volleyAccuracyBonus = 0.15f;

        [Header("Anti-Unit Bonuses")]
        public float bonusVsCavalry = 1f;
        public float bonusVsInfantry = 1f;
        public float bonusVsArtillery = 1f;

        [Header("Fatigue")]
        public float maxStamina = 100f;
        public float staminaDrainWalk = 1f;
        public float staminaDrainRun = 5f;
        public float staminaDrainCharge = 8f;
        public float staminaDrainCombat = 3f;
        public float staminaRecoveryIdle = 4f;
        public float fatigueSpeedPenalty = 0.4f;       // Speed multiplier at 0 stamina
        public float fatigueAccuracyPenalty = 0.3f;    // Accuracy multiplier at 0 stamina
        public float fatigueMoralePenalty = 0.5f;       // Morale recovery multiplier at 0 stamina

        [Header("Ammunition")]
        public int maxAmmo = 40;                        // Musket rounds per soldier
        public bool hasUnlimitedAmmo = false;           // Artillery resupply etc.
        public float splashRadius = 0f;                 // For artillery area damage

        [Header("Artillery - Canister Shot")]
        public float canisterRange = 60f;               // Range at which artillery auto-switches to canister
        public int canisterPellets = 12;                 // Number of projectiles in canister cone
        public float canisterDamage = 8f;                // Damage per canister pellet
        public float canisterConeAngle = 15f;            // Spread angle in degrees
        public float canisterSuppression = 25f;          // Massive suppression from canister

        [Header("Artillery - Limber/Unlimber")]
        public float limberedSpeedMultiplier = 2.5f;     // Speed multiplier when limbered (hitched to horses)
        public float limberTransitionTime = 4f;          // Seconds to limber/unlimber

        [Header("Skirmisher")]
        public float skirmishDamageReduction = 0.25f;   // 25% less ranged damage taken in skirmish
        public float skirmishFireOnMoveAccuracy = 0.5f;  // 50% accuracy when firing while moving
        public bool canSkirmish = false;                  // Only light infantry types can skirmish

        [Header("Suppression")]
        public float suppressionResistance = 0f;        // 0 = normal, 0.5 = elite resists 50% suppression
        public float suppressionRecoveryRate = 8f;       // Suppression decay per second
        public float suppressionPerHit = 12f;             // Suppression gained when hit
        public float suppressionPerNearMiss = 5f;         // Suppression from nearby shots

        [Header("Experience")]
        public float startingExperience = 0f;           // 0-100
        public float experiencePerKill = 10f;
        public float experiencePerVolley = 2f;
        public float veteranThreshold = 50f;            // Becomes veteran
        public float eliteThreshold = 80f;              // Becomes elite
        public float experienceAccuracyBonus = 0.15f;   // Max bonus at 100 XP
        public float experienceMoraleBonus = 20f;       // Max bonus at 100 XP

        [Header("Officers")]
        public bool hasOfficer = true;
        public float officerMoraleBonus = 15f;          // Morale bonus while officer alive
        public float officerRecoveryBonus = 2f;         // Extra morale recovery rate

        [Header("Regiment")]
        public int defaultRegimentSize = 60;
        public int maxRegimentSize = 120;

        [Header("Visuals")]
        public Color factionColor = Color.blue;
        public GameObject unitPrefab;
        public UnitVisualShape visualShape = UnitVisualShape.Capsule;
        public float visualScaleMultiplier = 1f;
    }

    public enum UnitVisualShape
    {
        Capsule,
        Cube,
        Cylinder,
        Sphere
    }
}
