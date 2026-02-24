using UnityEngine;

namespace NapoleonicWars.Data
{
    /// <summary>
    /// ScriptableObject holding all audio clips for the game.
    /// Create via Assets > Create > Napoleonic Wars > Sound Bank.
    /// If no AudioClips are assigned, the game runs silently (no errors).
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "Napoleonic Wars/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Musket")]
        public AudioClip[] musketFire;
        public AudioClip[] musketReload;
        public AudioClip musketDryFire;

        [Header("Cannon")]
        public AudioClip[] cannonFire;
        public AudioClip[] cannonImpact;

        [Header("Melee")]
        public AudioClip[] bayonetStab;
        public AudioClip[] swordClash;
        public AudioClip[] cavalryCharge;

        [Header("Unit Voices")]
        public AudioClip[] orderAcknowledge;
        public AudioClip[] chargeShout;
        public AudioClip[] deathCry;
        public AudioClip[] fleeScream;
        public AudioClip[] volleyCommand;

        [Header("Ambient")]
        public AudioClip[] battleAmbience;
        public AudioClip[] windAmbience;
        public AudioClip[] rainAmbience;

        [Header("Music")]
        public AudioClip[] battleMusic;
        public AudioClip[] victoryMusic;
        public AudioClip[] defeatMusic;
        public AudioClip[] menuMusic;
        public AudioClip[] campaignMusic;

        [Header("UI")]
        public AudioClip buttonClick;
        public AudioClip buttonHover;
        public AudioClip notificationSound;

        [Header("Drums & Fifes")]
        public AudioClip[] marchDrums;
        public AudioClip[] chargeDrums;
        public AudioClip[] retreatDrums;

        /// <summary>
        /// Get a random clip from an array, or null if empty.
        /// </summary>
        public static AudioClip GetRandom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}
