using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    public static class UnitFactory
    {
        public static UnitData CreateLineInfantry(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Line Infantry";
            data.unitType = UnitType.LineInfantry;
            data.faction = faction;
            data.maxHealth = 100f;
            data.moveSpeed = 3.5f;
            data.runSpeed = 6f;
            data.attackDamage = 25f;
            data.attackRange = 100f;
            data.attackCooldown = 3f;
            data.accuracy = 0.6f;
            data.meleeDamage = 15f;
            data.meleeRange = 1.5f;
            data.maxMorale = 100f;
            data.moraleLossOnHit = 5f;
            data.moraleLossOnDeath = 2f;
            data.moraleRecoveryRate = 1f;
            data.fleeThreshold = 20f;
            data.defaultRegimentSize = 60;
            data.canVolleyFire = true;
            data.volleyRanks = 2;
            data.volleyCooldown = 4f;
            data.volleyAccuracyBonus = 0.15f;
            data.canCharge = false;
            data.bonusVsCavalry = 1f;
            data.bonusVsInfantry = 1f;
            data.bonusVsArtillery = 1f;
            data.visualScaleMultiplier = 1f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateGrenadier(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Grenadier";
            data.unitType = UnitType.Grenadier;
            data.faction = faction;
            data.maxHealth = 140f;
            data.moveSpeed = 3f;
            data.runSpeed = 5.5f;
            data.attackDamage = 30f;
            data.attackRange = 90f;
            data.attackCooldown = 3f;
            data.accuracy = 0.65f;
            data.meleeDamage = 25f;
            data.meleeRange = 1.5f;
            data.maxMorale = 130f;
            data.moraleLossOnHit = 3f;
            data.moraleLossOnDeath = 1.5f;
            data.moraleRecoveryRate = 1.5f;
            data.fleeThreshold = 15f;
            data.defaultRegimentSize = 40;
            data.canVolleyFire = true;
            data.volleyRanks = 2;
            data.volleyCooldown = 3.5f;
            data.volleyAccuracyBonus = 0.1f;
            data.canCharge = false;
            data.bonusVsCavalry = 1f;
            data.bonusVsInfantry = 1.2f;
            data.bonusVsArtillery = 1f;
            data.visualScaleMultiplier = 1.15f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateCavalry(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Cavalry";
            data.unitType = UnitType.Cavalry;
            data.faction = faction;
            data.maxHealth = 120f;
            data.moveSpeed = 7f;
            data.runSpeed = 12f;
            data.attackDamage = 10f;
            data.attackRange = 2f;
            data.attackCooldown = 1.5f;
            data.accuracy = 0.8f;
            data.meleeDamage = 35f;
            data.meleeRange = 2f;
            data.maxMorale = 110f;
            data.moraleLossOnHit = 4f;
            data.moraleLossOnDeath = 3f;
            data.moraleRecoveryRate = 1.2f;
            data.fleeThreshold = 25f;
            data.defaultRegimentSize = 30;
            data.canVolleyFire = false;
            data.canCharge = true;
            data.chargeBonusDamage = 30f;
            data.chargeMinDistance = 10f;
            data.chargeSpeedMultiplier = 1.8f;
            data.flankingDamageMultiplier = 1.5f;
            data.rearDamageMultiplier = 2.5f;
            data.moraleLossOnCharged = 20f;
            data.bonusVsCavalry = 0.8f;
            data.bonusVsInfantry = 1.3f;
            data.bonusVsArtillery = 2f;
            data.visualScaleMultiplier = 1.2f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateArtillery(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Artillery";
            data.unitType = UnitType.Artillery;
            data.faction = faction;
            data.maxHealth = 80f;
            data.moveSpeed = 1.5f;
            data.runSpeed = 2.5f;
            data.attackDamage = 60f;
            data.attackRange = 360f;
            data.attackCooldown = 6f;
            data.accuracy = 0.45f;
            data.meleeDamage = 5f;
            data.meleeRange = 1.5f;
            data.maxMorale = 70f;
            data.moraleLossOnHit = 8f;
            data.moraleLossOnDeath = 5f;
            data.moraleRecoveryRate = 0.5f;
            data.fleeThreshold = 30f;
            data.defaultRegimentSize = 6;
            data.canVolleyFire = false;
            data.canCharge = false;
            data.splashRadius = 5f;            // Area damage radius
            data.maxAmmo = 30;                 // Cannonballs per gun
            data.hasUnlimitedAmmo = false;
            data.bonusVsCavalry = 0.5f;
            data.bonusVsInfantry = 1f;
            data.bonusVsArtillery = 0.5f;
            data.visualScaleMultiplier = 1.5f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateLightInfantry(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Light Infantry";
            data.unitType = UnitType.LightInfantry;
            data.faction = faction;
            data.maxHealth = 80f;
            data.moveSpeed = 4.5f;
            data.runSpeed = 7.5f;
            data.attackDamage = 22f;
            data.attackRange = 120f;
            data.attackCooldown = 2.5f;
            data.accuracy = 0.7f;
            data.meleeDamage = 10f;
            data.meleeRange = 1.5f;
            data.maxMorale = 85f;
            data.moraleLossOnHit = 6f;
            data.moraleLossOnDeath = 3f;
            data.moraleRecoveryRate = 1.2f;
            data.fleeThreshold = 25f;
            data.defaultRegimentSize = 40;
            data.canVolleyFire = true;
            data.volleyRanks = 1;
            data.volleyCooldown = 2.5f;
            data.volleyAccuracyBonus = 0.2f;
            data.canCharge = false;
            data.bonusVsCavalry = 0.8f;
            data.bonusVsInfantry = 1f;
            data.bonusVsArtillery = 1.2f;
            data.visualScaleMultiplier = 0.9f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateHussar(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Hussar";
            data.unitType = UnitType.Hussar;
            data.faction = faction;
            data.maxHealth = 110f;
            data.moveSpeed = 8f;
            data.runSpeed = 14f;
            data.attackDamage = 12f;
            data.attackRange = 2f;
            data.attackCooldown = 1.3f;
            data.accuracy = 0.75f;
            data.meleeDamage = 30f;
            data.meleeRange = 2f;
            data.maxMorale = 105f;
            data.moraleLossOnHit = 4f;
            data.moraleLossOnDeath = 3f;
            data.moraleRecoveryRate = 1.3f;
            data.fleeThreshold = 22f;
            data.defaultRegimentSize = 30;
            data.canVolleyFire = false;
            data.canCharge = true;
            data.chargeBonusDamage = 25f;
            data.chargeMinDistance = 8f;
            data.chargeSpeedMultiplier = 2.0f;
            data.flankingDamageMultiplier = 1.8f;
            data.rearDamageMultiplier = 2.5f;
            data.moraleLossOnCharged = 18f;
            data.bonusVsCavalry = 1f;
            data.bonusVsInfantry = 1.2f;
            data.bonusVsArtillery = 2.2f;
            data.visualScaleMultiplier = 1.15f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateLancer(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Lancer";
            data.unitType = UnitType.Lancer;
            data.faction = faction;
            data.maxHealth = 115f;
            data.moveSpeed = 7.5f;
            data.runSpeed = 13f;
            data.attackDamage = 8f;
            data.attackRange = 2.5f;
            data.attackCooldown = 1.4f;
            data.accuracy = 0.7f;
            data.meleeDamage = 38f;
            data.meleeRange = 2.5f;
            data.maxMorale = 108f;
            data.moraleLossOnHit = 4f;
            data.moraleLossOnDeath = 3f;
            data.moraleRecoveryRate = 1.2f;
            data.fleeThreshold = 23f;
            data.defaultRegimentSize = 30;
            data.canVolleyFire = false;
            data.canCharge = true;
            data.chargeBonusDamage = 35f;
            data.chargeMinDistance = 12f;
            data.chargeSpeedMultiplier = 1.9f;
            data.flankingDamageMultiplier = 1.6f;
            data.rearDamageMultiplier = 2.5f;
            data.moraleLossOnCharged = 22f;
            data.bonusVsCavalry = 1.2f;
            data.bonusVsInfantry = 1.4f;
            data.bonusVsArtillery = 1.8f;
            data.visualScaleMultiplier = 1.2f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateImperialGuard(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Imperial Guard";
            data.unitType = UnitType.GuardInfantry;
            data.faction = faction;
            data.maxHealth = 160f;
            data.moveSpeed = 3.5f;
            data.runSpeed = 6.5f;
            data.attackDamage = 35f;
            data.attackRange = 100f;
            data.attackCooldown = 2.5f;
            data.accuracy = 0.75f;
            data.meleeDamage = 35f;
            data.meleeRange = 1.5f;
            data.maxMorale = 160f;
            data.moraleLossOnHit = 2f;
            data.moraleLossOnDeath = 1f;
            data.moraleRecoveryRate = 2f;
            data.fleeThreshold = 8f;
            data.defaultRegimentSize = 40;
            data.canVolleyFire = true;
            data.volleyRanks = 3;
            data.volleyCooldown = 3f;
            data.volleyAccuracyBonus = 0.2f;
            data.canCharge = false;
            data.bonusVsCavalry = 1.2f;
            data.bonusVsInfantry = 1.3f;
            data.bonusVsArtillery = 1f;
            data.experienceAccuracyBonus = 0.2f;
            data.experienceMoraleBonus = 25f;
            data.startingExperience = 60f;
            data.visualScaleMultiplier = 1.2f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateGuardCavalry(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Guard Cavalry";
            data.unitType = UnitType.GuardCavalry;
            data.faction = faction;
            data.maxHealth = 150f;
            data.moveSpeed = 8f;
            data.runSpeed = 14f;
            data.attackDamage = 15f;
            data.attackRange = 2f;
            data.attackCooldown = 1.2f;
            data.accuracy = 0.85f;
            data.meleeDamage = 45f;
            data.meleeRange = 2.5f;
            data.maxMorale = 140f;
            data.moraleLossOnHit = 2.5f;
            data.moraleLossOnDeath = 2f;
            data.moraleRecoveryRate = 1.8f;
            data.fleeThreshold = 10f;
            data.defaultRegimentSize = 25;
            data.canVolleyFire = false;
            data.canCharge = true;
            data.chargeBonusDamage = 40f;
            data.chargeMinDistance = 10f;
            data.chargeSpeedMultiplier = 2.0f;
            data.flankingDamageMultiplier = 2.0f;
            data.rearDamageMultiplier = 3.0f;
            data.moraleLossOnCharged = 25f;
            data.bonusVsCavalry = 1.2f;
            data.bonusVsInfantry = 1.5f;
            data.bonusVsArtillery = 2.5f;
            data.startingExperience = 60f;
            data.visualScaleMultiplier = 1.3f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        public static UnitData CreateGuardArtillery(FactionType faction)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            data.unitName = GetFactionName(faction) + " Guard Artillery";
            data.unitType = UnitType.GuardArtillery;
            data.faction = faction;
            data.maxHealth = 100f;
            data.moveSpeed = 2f;
            data.runSpeed = 3f;
            data.attackDamage = 80f;
            data.attackRange = 450f;
            data.attackCooldown = 5f;
            data.accuracy = 0.55f;
            data.meleeDamage = 8f;
            data.meleeRange = 1.5f;
            data.maxMorale = 90f;
            data.moraleLossOnHit = 6f;
            data.moraleLossOnDeath = 4f;
            data.moraleRecoveryRate = 0.8f;
            data.fleeThreshold = 20f;
            data.defaultRegimentSize = 6;
            data.canVolleyFire = false;
            data.canCharge = false;
            data.splashRadius = 7f;
            data.maxAmmo = 40;
            data.hasUnlimitedAmmo = false;
            data.bonusVsCavalry = 0.6f;
            data.bonusVsInfantry = 1.2f;
            data.bonusVsArtillery = 0.6f;
            data.startingExperience = 60f;
            data.visualScaleMultiplier = 1.6f;
            data.factionColor = GetFactionColor(faction);
            return data;
        }

        /// <summary>Create UnitData for any UnitType. Dispatches to specific factory method.</summary>
        public static UnitData CreateUnitData(UnitType type, FactionType faction)
        {
            return type switch
            {
                UnitType.LineInfantry => CreateLineInfantry(faction),
                UnitType.LightInfantry => CreateLightInfantry(faction),
                UnitType.Grenadier => CreateGrenadier(faction),
                UnitType.Cavalry => CreateCavalry(faction),
                UnitType.Hussar => CreateHussar(faction),
                UnitType.Lancer => CreateLancer(faction),
                UnitType.Artillery => CreateArtillery(faction),
                UnitType.GuardInfantry => CreateImperialGuard(faction),
                UnitType.GuardCavalry => CreateGuardCavalry(faction),
                UnitType.GuardArtillery => CreateGuardArtillery(faction),
                _ => CreateLineInfantry(faction)
            };
        }

        private static string GetFactionName(FactionType faction)
        {
            switch (faction)
            {
                case FactionType.France: return "French";
                case FactionType.Britain: return "British";
                case FactionType.Prussia: return "Prussian";
                case FactionType.Russia: return "Russian";
                case FactionType.Austria: return "Austrian";
                default: return "Unknown";
            }
        }

        public static Color GetFactionColor(FactionType faction)
        {
            switch (faction)
            {
                case FactionType.France: return new Color(0.2f, 0.3f, 0.8f);    // Blue
                case FactionType.Britain: return new Color(0.8f, 0.2f, 0.2f);    // Red
                case FactionType.Prussia: return new Color(0.15f, 0.15f, 0.4f);  // Dark blue
                case FactionType.Russia: return new Color(0.2f, 0.5f, 0.2f);     // Green
                case FactionType.Austria: return new Color(0.9f, 0.9f, 0.85f);   // White
                default: return Color.gray;
            }
        }
    }
}
