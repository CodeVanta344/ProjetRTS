using UnityEngine;

namespace NapoleonicWars.Data
{
    public enum UnitType
    {
        LineInfantry,
        LightInfantry,
        Grenadier,
        Cavalry,
        Lancer,
        Hussar,
        Artillery,
        
        // Elite Guard Units (Late Game)
        ImperialGuard,      // Napoleon's Old Guard - ultimate infantry
        GuardCavalry,       // Elite mounted troops
        GuardArtillery      // Emperor's personal guns
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
