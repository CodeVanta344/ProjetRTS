using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Manages all battle audio: combat SFX, ambient sounds, and music.
    /// Requires a SoundBank ScriptableObject (optional — runs silently without one).
    /// </summary>
    public class BattleSoundManager : MonoBehaviour
    {
        public static BattleSoundManager Instance { get; private set; }

        [Header("Sound Bank")]
        [SerializeField] private SoundBank soundBank;

        [Header("Volume")]
        [SerializeField] private float masterVolume = 1f;
        [SerializeField] private float sfxVolume = 0.8f;
        [SerializeField] private float musicVolume = 0.4f;
        [SerializeField] private float ambienceVolume = 0.3f;

        [Header("Pooling")]
        [SerializeField] private int sfxPoolSize = 20;

        private AudioSource musicSource;
        private AudioSource ambienceSource;
        private AudioSource drumsSource;
        private AudioSource[] sfxPool;
        private int sfxPoolIndex;

        // SFX throttling — prevent hundreds of identical sounds per second
        private float lastMusketTime;
        private float lastBayonetTime;
        private float lastDeathTime;
        private const float MusketCooldown = 0.05f;
        private const float BayonetCooldown = 0.08f;
        private const float DeathCooldown = 0.1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            // Music
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = musicVolume * masterVolume;
            musicSource.priority = 0;

            // Ambience
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.loop = true;
            ambienceSource.playOnAwake = false;
            ambienceSource.volume = ambienceVolume * masterVolume;
            ambienceSource.priority = 10;

            // Drums
            drumsSource = gameObject.AddComponent<AudioSource>();
            drumsSource.loop = true;
            drumsSource.playOnAwake = false;
            drumsSource.volume = sfxVolume * masterVolume * 0.6f;
            drumsSource.priority = 20;

            // SFX pool
            sfxPool = new AudioSource[sfxPoolSize];
            for (int i = 0; i < sfxPoolSize; i++)
            {
                sfxPool[i] = gameObject.AddComponent<AudioSource>();
                sfxPool[i].playOnAwake = false;
                sfxPool[i].spatialBlend = 1f; // 3D sound
                sfxPool[i].rolloffMode = AudioRolloffMode.Linear;
                sfxPool[i].minDistance = 5f;
                sfxPool[i].maxDistance = 100f;
                sfxPool[i].priority = 128;
            }
        }

        private void Start()
        {
            // Try to load SoundBank from Resources if not assigned
            if (soundBank == null)
                soundBank = Resources.Load<SoundBank>("SoundBank");

            // Music and ambience disabled — only weapon SFX active
        }

        // ============================================================
        // MUSIC
        // ============================================================

        public void PlayBattleMusic() { /* Disabled */ }

        public void PlayVictoryMusic() { /* Disabled */ }

        public void PlayDefeatMusic() { /* Disabled */ }

        public void StopMusic()
        {
            musicSource.Stop();
        }

        // ============================================================
        // AMBIENCE
        // ============================================================

        public void PlayBattleAmbience() { /* Disabled */ }

        public void PlayRainAmbience() { /* Disabled */ }

        // ============================================================
        // DRUMS
        // ============================================================

        public void PlayMarchDrums() { /* Disabled */ }

        public void PlayChargeDrums() { /* Disabled */ }

        public void StopDrums()
        {
            drumsSource.Stop();
        }

        // ============================================================
        // COMBAT SFX (3D positional)
        // ============================================================

        public void PlayMusketFire(Vector3 position)
        {
            if (soundBank == null) return;
            if (Time.time - lastMusketTime < MusketCooldown) return;
            lastMusketTime = Time.time;
            PlaySFXAtPosition(SoundBank.GetRandom(soundBank.musketFire), position, sfxVolume);
        }

        public void PlayCannonFire(Vector3 position)
        {
            if (soundBank == null) return;
            PlaySFXAtPosition(SoundBank.GetRandom(soundBank.cannonFire), position, sfxVolume * 1.2f);
        }

        public void PlayCannonImpact(Vector3 position)
        {
            if (soundBank == null) return;
            PlaySFXAtPosition(SoundBank.GetRandom(soundBank.cannonImpact), position, sfxVolume);
        }

        public void PlayBayonetStab(Vector3 position)
        {
            if (soundBank == null) return;
            if (Time.time - lastBayonetTime < BayonetCooldown) return;
            lastBayonetTime = Time.time;
            PlaySFXAtPosition(SoundBank.GetRandom(soundBank.bayonetStab), position, sfxVolume * 0.8f);
        }

        public void PlaySwordClash(Vector3 position)
        {
            if (soundBank == null) return;
            PlaySFXAtPosition(SoundBank.GetRandom(soundBank.swordClash), position, sfxVolume * 0.8f);
        }

        public void PlayCavalryCharge(Vector3 position) { /* Disabled */ }

        public void PlayDeathCry(Vector3 position) { /* Disabled */ }

        public void PlayChargeShout(Vector3 position) { /* Disabled */ }

        public void PlayVolleyCommand(Vector3 position) { /* Disabled */ }

        public void PlayOrderAcknowledge(Vector3 position) { /* Disabled */ }

        // ============================================================
        // UI SFX (2D)
        // ============================================================

        public void PlayButtonClick()
        {
            if (soundBank == null || soundBank.buttonClick == null) return;
            PlaySFX2D(soundBank.buttonClick, sfxVolume * 0.5f);
        }

        public void PlayNotification()
        {
            if (soundBank == null || soundBank.notificationSound == null) return;
            PlaySFX2D(soundBank.notificationSound, sfxVolume * 0.6f);
        }

        // ============================================================
        // CORE PLAYBACK
        // ============================================================

        private void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;

            AudioSource source = GetNextSFXSource();
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.volume = volume * masterVolume;
            source.pitch = Random.Range(0.95f, 1.05f); // Slight variation
            source.clip = clip;
            source.Play();
        }

        private void PlaySFX2D(AudioClip clip, float volume)
        {
            if (clip == null) return;

            AudioSource source = GetNextSFXSource();
            source.spatialBlend = 0f;
            source.volume = volume * masterVolume;
            source.pitch = 1f;
            source.clip = clip;
            source.Play();
        }

        private AudioSource GetNextSFXSource()
        {
            AudioSource source = sfxPool[sfxPoolIndex];
            sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
            return source;
        }

        // ============================================================
        // VOLUME CONTROL
        // ============================================================

        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            UpdateVolumes();
        }

        public void SetMusicVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            UpdateVolumes();
        }

        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            UpdateVolumes();
        }

        private void UpdateVolumes()
        {
            musicSource.volume = musicVolume * masterVolume;
            ambienceSource.volume = ambienceVolume * masterVolume;
            drumsSource.volume = sfxVolume * masterVolume * 0.6f;
        }
    }
}
