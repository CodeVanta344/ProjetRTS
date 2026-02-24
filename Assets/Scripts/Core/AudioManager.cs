using UnityEngine;

namespace NapoleonicWars.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Volume Settings")]
        [SerializeField] private float masterVolume = 1f;
        [SerializeField] private float sfxVolume = 0.8f;
        [SerializeField] private float musicVolume = 0.5f;

        [Header("Audio Sources")]
        private AudioSource musicSource;
        private AudioSource ambientSource;

        // Pooled SFX sources
        private AudioSource[] sfxSources;
        private int sfxIndex;
        private const int SFX_POOL_SIZE = 16;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Music source
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = musicVolume * masterVolume;

            // Ambient source
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.volume = sfxVolume * masterVolume * 0.3f;

            // SFX pool
            sfxSources = new AudioSource[SFX_POOL_SIZE];
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                sfxSources[i] = gameObject.AddComponent<AudioSource>();
                sfxSources[i].playOnAwake = false;
                sfxSources[i].spatialBlend = 0.8f;
            }
        }

        private void Start()
        {
            GenerateAndPlayAmbient();
        }

        public void PlaySFX(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;

            AudioSource source = sfxSources[sfxIndex];
            sfxIndex = (sfxIndex + 1) % SFX_POOL_SIZE;

            source.transform.position = position;
            source.clip = clip;
            source.volume = sfxVolume * masterVolume * volumeScale;
            source.pitch = Random.Range(0.9f, 1.1f);
            source.Play();
        }

        public void PlaySFXAtPoint(Vector3 position, float volumeScale = 1f)
        {
            // Generate a simple procedural gunshot sound
            AudioSource source = sfxSources[sfxIndex];
            sfxIndex = (sfxIndex + 1) % SFX_POOL_SIZE;

            source.transform.position = position;
            source.volume = sfxVolume * masterVolume * volumeScale * 0.3f;
            source.pitch = Random.Range(0.7f, 1.3f);

            AudioClip clip = GenerateGunshotClip();
            if (clip != null)
            {
                source.clip = clip;
                source.Play();
            }
        }

        public void PlayChargeSFX(Vector3 position)
        {
            AudioSource source = sfxSources[sfxIndex];
            sfxIndex = (sfxIndex + 1) % SFX_POOL_SIZE;

            source.transform.position = position;
            source.volume = sfxVolume * masterVolume * 0.5f;
            source.pitch = Random.Range(0.8f, 1.0f);

            AudioClip clip = GenerateChargeClip();
            if (clip != null)
            {
                source.clip = clip;
                source.Play();
            }
        }

        public void PlayCannonSFX(Vector3 position)
        {
            AudioSource source = sfxSources[sfxIndex];
            sfxIndex = (sfxIndex + 1) % SFX_POOL_SIZE;

            source.transform.position = position;
            source.volume = sfxVolume * masterVolume * 0.6f;
            source.pitch = Random.Range(0.6f, 0.8f);

            AudioClip clip = GenerateCannonClip();
            if (clip != null)
            {
                source.clip = clip;
                source.Play();
            }
        }

        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            UpdateVolumes();
        }

        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            UpdateVolumes();
        }

        public void SetMusicVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            UpdateVolumes();
        }

        private void UpdateVolumes()
        {
            if (musicSource != null)
                musicSource.volume = musicVolume * masterVolume;
            if (ambientSource != null)
                ambientSource.volume = sfxVolume * masterVolume * 0.3f;
        }

        // === PROCEDURAL AUDIO ===

        private AudioClip GenerateGunshotClip()
        {
            int sampleRate = 22050;
            int samples = sampleRate / 8;
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 40f);
                float noise = Random.Range(-1f, 1f);
                data[i] = noise * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create("gunshot", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateCannonClip()
        {
            int sampleRate = 22050;
            int samples = sampleRate / 3;
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 8f);
                float noise = Random.Range(-1f, 1f);
                float bass = Mathf.Sin(2f * Mathf.PI * 60f * t);
                data[i] = (noise * 0.6f + bass * 0.4f) * envelope * 0.7f;
            }

            AudioClip clip = AudioClip.Create("cannon", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateChargeClip()
        {
            int sampleRate = 22050;
            int samples = sampleRate / 2;
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Clamp01(t * 4f) * Mathf.Exp(-t * 3f);
                float horn = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.3f;
                float horn2 = Mathf.Sin(2f * Mathf.PI * 554f * t) * 0.2f;
                data[i] = (horn + horn2) * envelope;
            }

            AudioClip clip = AudioClip.Create("charge", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void GenerateAndPlayAmbient()
        {
            int sampleRate = 22050;
            int samples = sampleRate * 5;
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float wind = Mathf.PerlinNoise(t * 0.5f, 0f) * 0.15f;
                float birds = Mathf.Sin(2f * Mathf.PI * (2000f + Mathf.Sin(t * 3f) * 500f) * t) *
                              Mathf.Max(0f, Mathf.Sin(t * 2f)) * 0.02f;
                data[i] = wind + birds;
            }

            AudioClip clip = AudioClip.Create("ambient", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            ambientSource.clip = clip;
            ambientSource.Play();
        }
    }
}
